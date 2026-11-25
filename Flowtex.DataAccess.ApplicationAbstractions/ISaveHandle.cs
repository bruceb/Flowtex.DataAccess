namespace Flowtex.DataAccess.Abstractions;

/// <summary>
/// Represents a deferred save operation returned by a single mutation method
/// (AddAsync, Update, Remove). Calling <see cref="SaveAsync"/> flushes
/// <em>all</em> pending changes tracked by the underlying EF context, not
/// only the staged operation, because EF Core shares a single change tracker
/// per context instance.
/// <para>
/// When staging multiple mutations that should be committed in one round-trip,
/// call each mutation method, ignore the returned handles, and then call
/// <see cref="IDataStore.SaveChangesAsync"/> directly.
/// </para>
/// </summary>
public interface ISaveHandle
{
    /// <summary>
    /// Persists all pending changes on the store that issued this handle.
    /// Equivalent to calling <see cref="IDataStore.SaveChangesAsync"/>.
    /// </summary>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveAsync(CancellationToken ct = default);
}