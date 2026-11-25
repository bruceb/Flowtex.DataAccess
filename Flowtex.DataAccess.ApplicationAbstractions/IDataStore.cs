using System.Linq.Expressions;

namespace Flowtex.DataAccess.Abstractions;

/// <summary>
/// Read/write data access surface: everything on <see cref="IReadStore"/> plus tracked queries,
/// instance-based writes, set-based server operations, unit-of-work saves and transactions.
/// </summary>
/// <remarks>
/// An implementation wraps a single EF Core <c>DbContext</c>, so one instance represents one unit
/// of work. Register it with a scoped lifetime (one per request or operation).
/// </remarks>
public interface IDataStore : IReadStore
{
    /// <summary>
    /// Returns a change-tracked <see cref="IQueryable{T}"/>. Entities loaded through it can be
    /// modified in place and persisted with <see cref="SaveChangesAsync"/>, without calling
    /// <see cref="Update{T}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    IQueryable<T> QueryTracked<T>() where T : class;

    /// <summary>
    /// Stages <paramref name="entity"/> for insertion. Nothing is written until the returned
    /// handle's <see cref="ISaveHandle.SaveAsync"/> or <see cref="SaveChangesAsync"/> is called.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The new entity to insert.</param>
    /// <param name="ct">Checked before staging: if already cancelled, nothing is staged and the call is cancelled. Staging does no I/O, so pass a token to the save call too.</param>
    /// <returns>A handle that saves <em>all</em> pending changes on this store.</returns>
    ValueTask<ISaveHandle> AddAsync<T>(T entity, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stages <paramref name="entities"/> for insertion. Nothing is written until the returned
    /// handle's <see cref="ISaveHandle.SaveAsync"/> or <see cref="SaveChangesAsync"/> is called.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The new entities to insert.</param>
    /// <param name="ct">Checked before staging: if already cancelled, nothing is staged and the call is cancelled. Staging does no I/O, so pass a token to the save call too.</param>
    /// <returns>A handle that saves <em>all</em> pending changes on this store.</returns>
    ValueTask<ISaveHandle> AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Attaches a <em>detached</em> entity and marks every property as modified.
    /// </summary>
    /// <remarks>
    /// Do not call this for entities loaded through <see cref="QueryTracked{T}"/>: they are already
    /// tracked, and calling <see cref="SaveChangesAsync"/> writes only the columns that changed.
    /// </remarks>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The detached entity whose current values should be written.</param>
    /// <returns>A handle that saves <em>all</em> pending changes on this store.</returns>
    ISaveHandle Update<T>(T entity) where T : class;

    /// <summary>
    /// Stages <paramref name="entity"/> for deletion. Nothing is written until the returned
    /// handle's <see cref="ISaveHandle.SaveAsync"/> or <see cref="SaveChangesAsync"/> is called.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>A handle that saves <em>all</em> pending changes on this store.</returns>
    ISaveHandle Remove<T>(T entity) where T : class;

    /// <summary>
    /// Deletes every row matching <paramref name="predicate"/> with a single server-side
    /// <c>DELETE</c> statement, executed immediately.
    /// </summary>
    /// <remarks>
    /// Bypasses the change tracker: entities already loaded in this store are not removed from it, and
    /// no <c>SaveChanges</c> is needed or performed. If a deleted row is also loaded through
    /// <see cref="QueryTracked{T}"/> and then modified or removed, a later <see cref="SaveChangesAsync"/>
    /// throws <c>DbUpdateConcurrencyException</c>, because the row no longer exists. Not supported by
    /// the EF Core InMemory provider.
    /// </remarks>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Selects the rows to delete.</param>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> ExecuteDeleteAsync<T>(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default) where T : class;

    /// <summary>
    /// Updates every row matching <paramref name="predicate"/> with a single server-side
    /// <c>UPDATE</c> statement, executed immediately.
    /// </summary>
    /// <remarks>
    /// Bypasses the change tracker: entities already loaded in this store are not updated, and no
    /// <c>SaveChanges</c> is needed or performed. If a matching row is also loaded and modified
    /// through <see cref="QueryTracked{T}"/>, a later <see cref="SaveChangesAsync"/> writes the tracked
    /// values back over this update. Not supported by the EF Core InMemory provider.
    /// </remarks>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Selects the rows to update.</param>
    /// <param name="build">Declares the columns to set; at least one <c>Set</c> or <c>SetConst</c> call is required.</param>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="build"/> declared no columns.</exception>
    Task<int> ExecuteUpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Action<IUpdateBuilder<T>> build,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Persists every pending change (inserts, updates and deletes) on this store in one round-trip.
    /// </summary>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction, committing if it completes and
    /// rolling back if it throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No implicit save happens: call <see cref="SaveChangesAsync"/> (or a handle's
    /// <see cref="ISaveHandle.SaveAsync"/>) inside <paramref name="work"/>.
    /// </para>
    /// <para>
    /// If <paramref name="work"/> or the commit throws, the transaction is rolled back and the store's
    /// change tracker is cleared, whether or not anything was saved: the failed unit of work is
    /// discarded, so entities saved inside it (which no longer exist in the database) and unsaved
    /// edits (including ones made before this call) cannot be persisted by a later save. Load
    /// entities again before modifying them.
    /// </para>
    /// <para>
    /// The work runs through the provider's execution strategy. With a retrying strategy (for example
    /// <c>EnableRetryOnFailure</c>), <paramref name="work"/> may run more than once, so every attempt,
    /// including the first, starts with an empty change tracker; entities tracked before the call
    /// are detached. Load and modify entities inside <paramref name="work"/> rather than capturing
    /// instances from outside it. A failure during commit can be ambiguous (the commit may have
    /// succeeded), so a retried attempt should tolerate its earlier writes.
    /// </para>
    /// <para>
    /// Calling this method while a transaction is already active on the same store throws.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The value produced by <paramref name="work"/>.</typeparam>
    /// <param name="work">The operations to run; receives this store. Capture <paramref name="ct"/> to cancel work inside it.</param>
    /// <param name="ct">Cancels the execution strategy (including retry delays), starting the transaction and committing it.</param>
    /// <returns>The value returned by <paramref name="work"/>.</returns>
    /// <exception cref="InvalidOperationException">The execution strategy retries on failure and the store has unsaved changes.</exception>
    Task<TResult> InTransactionAsync<TResult>(Func<IDataStore, Task<TResult>> work, CancellationToken ct = default);
}
