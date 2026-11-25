using System.Linq.Expressions;
using Flowtex.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Flowtex.DataAccess.Infrastructure;

/// <summary>
/// Base implementation containing all generic data store operations.
/// Derived classes only need to supply the <see cref="Context"/> property.
/// </summary>
public abstract class DataStoreBase : IDataStore
{
    /// <summary>
    /// The EF Core context this store operates on. Every operation goes through
    /// <c>Context.Set&lt;T&gt;()</c>, so no per-entity <c>DbSet</c> properties are required.
    /// </summary>
    /// <remarks>
    /// Return the same scoped instance for the lifetime of the store; the store is a unit of work
    /// over that context.
    /// </remarks>
    protected abstract DbContext Context { get; }

    // READS (no-tracking by default)

    /// <inheritdoc/>
    public IQueryable<T> Query<T>() where T : class =>
        Context.Set<T>().AsNoTracking();

    /// <inheritdoc/>
    public IQueryable<TResult> Query<T, TResult>(Expression<Func<T, TResult>> selector) where T : class =>
        Context.Set<T>().AsNoTracking().Select(selector);

    /// <inheritdoc/>
    public Task<List<T>> ListAsync<T>(Func<IQueryable<T>, IQueryable<T>>? shape = null, CancellationToken ct = default) where T : class
    {
        var q = Context.Set<T>().AsNoTracking();
        if (shape is not null) q = shape(q);
        return q.ToListAsync(ct);
    }

    /// <inheritdoc/>
    public Task<List<TResult>> ListAsync<T, TResult>(Func<IQueryable<T>, IQueryable<TResult>> shape, CancellationToken ct = default) where T : class =>
        shape(Context.Set<T>().AsNoTracking()).ToListAsync(ct);

    /// <inheritdoc/>
    /// <remarks>
    /// Uses the EF Core identity-map (change-tracker cache) before issuing a database
    /// round-trip. If a tracked entity with the same key is already in scope it will be
    /// returned — even though <see cref="IReadStore"/> is otherwise a no-tracking surface.
    /// This is EF Core's designed behaviour; be aware when mixing tracked and untracked
    /// access within the same scope.
    /// </remarks>
    public ValueTask<T?> FindAsync<T>(object[] keyValues, CancellationToken ct = default) where T : class =>
        Context.Set<T>().FindAsync(keyValues, ct);

    /// <inheritdoc/>
    public IQueryable<T> QueryTracked<T>() where T : class =>
        Context.Set<T>(); // tracked by default

    // WRITES (instance-based)

    // These call the synchronous DbSet.Add/AddRange: EF Core's AddAsync only matters when a value
    // generator needs a database round-trip (e.g. HiLo sequences), and otherwise adds overhead.
    // Staging does no I/O, so ct is only checked up front: an already-cancelled call stages nothing.
    // The returned handle takes its own token in SaveAsync(ct).

    /// <inheritdoc/>
    public ValueTask<ISaveHandle> AddAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<ISaveHandle>(ct);
        Context.Set<T>().Add(entity);
        return new ValueTask<ISaveHandle>(new DelegatingSaveHandle(ct2 => Context.SaveChangesAsync(ct2)));
    }

    /// <inheritdoc/>
    public ValueTask<ISaveHandle> AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<ISaveHandle>(ct);
        Context.Set<T>().AddRange(entities);
        return new ValueTask<ISaveHandle>(new DelegatingSaveHandle(ct2 => Context.SaveChangesAsync(ct2)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>DbSet.Update</c>, which marks <em>all</em> properties as modified
    /// regardless of what changed. For entities already loaded via
    /// <see cref="QueryTracked{T}"/>, calling <see cref="SaveChangesAsync"/> directly
    /// (without calling Update) is more efficient: EF will detect only actual changes
    /// and emit a minimal UPDATE statement. Reserve this method for detached entities
    /// that were not loaded in the current scope.
    /// </remarks>
    public ISaveHandle Update<T>(T entity) where T : class
    {
        Context.Set<T>().Update(entity);
        return new DelegatingSaveHandle(ct => Context.SaveChangesAsync(ct));
    }

    /// <inheritdoc/>
    public ISaveHandle Remove<T>(T entity) where T : class
    {
        Context.Set<T>().Remove(entity);
        return new DelegatingSaveHandle(ct => Context.SaveChangesAsync(ct));
    }

    // SET-BASED SERVER OPS

    /// <inheritdoc/>
    public Task<int> ExecuteDeleteAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default) where T : class =>
        Context.Set<T>().Where(predicate).ExecuteDeleteAsync(ct);

    /// <inheritdoc/>
    public Task<int> ExecuteUpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Action<IUpdateBuilder<T>> build,
        CancellationToken ct = default) where T : class
    {
        var efBuilder = new EfUpdateBuilder<T>();
        build(efBuilder);
        var setter = efBuilder.Build();
        return Context.Set<T>().Where(predicate).ExecuteUpdateAsync(setter, ct);
    }

    // UOW / TRANSACTIONS

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        Context.SaveChangesAsync(ct);

    /// <inheritdoc/>
    public async Task<TResult> InTransactionAsync<TResult>(
        Func<IDataStore, Task<TResult>> work,
        CancellationToken ct = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();

        // A retry must start from the same state as the first attempt. Only an empty tracker can be
        // rebuilt identically, so with a retrying strategy every attempt starts empty, and unsaved
        // changes made before the call are rejected rather than lost on a retry.
        if (strategy.RetriesOnFailure && Context.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "InTransactionAsync cannot run with unsaved changes when the execution strategy retries on " +
                "failure, because a retry could not reapply them. Save or discard them first, or make them " +
                "inside the work delegate.");

        return await strategy.ExecuteAsync(async attemptCt =>
        {
            if (strategy.RetriesOnFailure) Context.ChangeTracker.Clear();

            // await using disposes (and therefore rolls back) the transaction if CommitAsync is
            // never reached. No implicit SaveChangesAsync here: callers save inside the delegate.
            await using var tx = await Context.Database.BeginTransactionAsync(attemptCt);
            try
            {
                var result = await work(this);
                await tx.CommitAsync(attemptCt);
                return result;
            }
            catch
            {
                // The unit of work failed, so discard all of its tracked state: entities saved in the
                // attempt were rolled back, and unsaved edits must not be persisted by a later save.
                Context.ChangeTracker.Clear();
                throw;
            }
        }, ct);
    }

    // Private helpers

    private sealed class DelegatingSaveHandle : ISaveHandle
    {
        private readonly Func<CancellationToken, Task<int>> _save;
        public DelegatingSaveHandle(Func<CancellationToken, Task<int>> save) => _save = save;
        public Task<int> SaveAsync(CancellationToken ct = default) => _save(ct);
    }

    // Internal EF adapter for IUpdateBuilder<T>
    private sealed class EfUpdateBuilder<T> : IUpdateBuilder<T> where T : class
    {
        private readonly List<Action<UpdateSettersBuilder<T>>> _setters = new();

        public IUpdateBuilder<T> Set<TProp>(
            Expression<Func<T, TProp>> property,
            Expression<Func<T, TProp>> value)
        {
            _setters.Add(s => s.SetProperty(property, value));
            return this;
        }

        public IUpdateBuilder<T> SetConst<TProp>(
            Expression<Func<T, TProp>> property,
            TProp value)
        {
            _setters.Add(s => s.SetProperty(property, value));
            return this;
        }

        internal Action<UpdateSettersBuilder<T>> Build()
        {
            if (_setters.Count == 0)
                throw new InvalidOperationException(
                    "ExecuteUpdateAsync requires at least one Set or SetConst call on the update builder.");

            return setters =>
            {
                foreach (var setter in _setters)
                    setter(setters);
            };
        }
    }
}
