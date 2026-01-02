using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Flowtex.DataAccess.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Flowtex.DataAccess.Infrastructure;

/// <summary>
/// Base implementation containing all generic data store operations.
/// Derived classes only need to provide context-specific operations.
/// </summary>
public abstract class DataStoreBase : IDataStore
{
    // Abstract methods that derived classes must implement
    protected abstract DbContext Context { get; }
    protected abstract DbSet<T> GetDbSet<T>() where T : class;

    // READS (no-tracking by default)

    public IQueryable<T> Query<T>() where T : class =>
        GetDbSet<T>().AsNoTracking();

    public IQueryable<TResult> Query<T, TResult>(Expression<Func<T, TResult>> selector) where T : class =>
        GetDbSet<T>().AsNoTracking().Select(selector);

    public Task<List<T>> ListAsync<T>(Func<IQueryable<T>, IQueryable<T>>? shape = null, CancellationToken ct = default) where T : class
    {
        var q = GetDbSet<T>().AsNoTracking();
        if (shape is not null) q = shape(q);
        return q.ToListAsync(ct);
    }

    public Task<List<TResult>> ListAsync<T, TResult>(Func<IQueryable<T>, IQueryable<TResult>> shape, CancellationToken ct = default) where T : class =>
        shape(GetDbSet<T>().AsNoTracking()).ToListAsync(ct);

    // When you DO want tracking
    public IQueryable<T> QueryTracked<T>() where T : class =>
        GetDbSet<T>(); // tracked by default

    // WRITES (instance-based)
    public async ValueTask<ISaveHandle> AddAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await GetDbSet<T>().AddAsync(entity, ct);
        return new DelegatingSaveHandle(ct2 => Context.SaveChangesAsync(ct2));
    }

    public async ValueTask<ISaveHandle> AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
    {
        await GetDbSet<T>().AddRangeAsync(entities, ct);
        return new DelegatingSaveHandle(ct2 => Context.SaveChangesAsync(ct2));
    }

    public ISaveHandle Update<T>(T entity) where T : class
    {
        GetDbSet<T>().Update(entity);
        return new DelegatingSaveHandle(ct => Context.SaveChangesAsync(ct));
    }

    public ISaveHandle Remove<T>(T entity) where T : class
    {
        GetDbSet<T>().Remove(entity);
        return new DelegatingSaveHandle(ct => Context.SaveChangesAsync(ct));
    }

    // SET-BASED SERVER OPS

    public Task<int> ExecuteDeleteAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default) where T : class =>
        GetDbSet<T>().Where(predicate).ExecuteDeleteAsync(ct);

    public Task<int> ExecuteUpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Action<IUpdateBuilder<T>> build,
        CancellationToken ct = default) where T : class
    {
        var efBuilder = new EfUpdateBuilder<T>();
        build(efBuilder);
        var setter = efBuilder.Build();
        return GetDbSet<T>().Where(predicate).ExecuteUpdateAsync(setter, ct);
    }

    // UOW / TRANSACTIONS

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        Context.SaveChangesAsync(ct);

    public async Task<TResult> InTransactionAsync<TResult>(
        Func<IDataStore, Task<TResult>> work,
        CancellationToken ct = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Context.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await work(this);
                await Context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
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
            return setters =>
            {
                foreach (var setter in _setters)
                {
                    setter(setters);
                }
            };
        }
    }
}
