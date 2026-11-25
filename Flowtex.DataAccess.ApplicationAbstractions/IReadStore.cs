using System.Linq.Expressions;

namespace Flowtex.DataAccess.Abstractions;

/// <summary>
/// Read-only data access surface. <see cref="Query{T}"/> and <see cref="ListAsync{T}"/> are
/// no-tracking, so their results are detached snapshots that are cheap to materialize and are not
/// saved back by <see cref="IDataStore.SaveChangesAsync"/>. <see cref="FindAsync{T}"/> is the
/// exception: it can return an entity the store is already tracking.
/// </summary>
/// <remarks>
/// Depend on this interface in query-side code that should not be able to write. If the same scoped
/// instance also serves as an <see cref="IDataStore"/>, do not mutate entities returned by
/// <see cref="FindAsync{T}"/>: a later save on that store would persist the change.
/// </remarks>
public interface IReadStore
{
    /// <summary>
    /// Returns a no-tracking <see cref="IQueryable{T}"/> for ad-hoc queries.
    /// The no-tracking convention is applied by the infrastructure layer.
    /// Opt into tracking explicitly via <see cref="IDataStore.QueryTracked{T}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    IQueryable<T> Query<T>() where T : class;

    /// <summary>
    /// Returns a no-tracking query projected through <paramref name="selector"/>. Projecting to a DTO
    /// fetches only the selected columns.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TResult">The projected type.</typeparam>
    /// <param name="selector">The projection, translated to SQL.</param>
    IQueryable<TResult> Query<T, TResult>(Expression<Func<T, TResult>> selector) where T : class;

    /// <summary>
    /// Executes a no-tracking query and materializes the results.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="shape">Optional filtering, ordering, paging or includes applied to the query; <see langword="null"/> returns every row.</param>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The matching entities.</returns>
    Task<List<T>> ListAsync<T>(Func<IQueryable<T>, IQueryable<T>>? shape = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Executes a no-tracking query that ends in a projection and materializes the results.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TResult">The projected type.</typeparam>
    /// <param name="shape">Builds the query, typically ending with <c>Select</c>.</param>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The projected results.</returns>
    Task<List<TResult>> ListAsync<T, TResult>(Func<IQueryable<T>, IQueryable<TResult>> shape, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Looks up an entity by primary key. Uses the identity-map cache before
    /// issuing a database round-trip, making it more efficient than
    /// <see cref="Query{T}"/> for single-key lookups.
    /// </summary>
    /// <remarks>
    /// Unlike the other members of this interface, the result can be a <em>tracked</em> entity: if one
    /// with the same key is already tracked by the store, that instance is returned.
    /// </remarks>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="keyValues">The primary key values, in key-property order.</param>
    /// <param name="ct">Cancels the database call.</param>
    /// <returns>The entity, or <see langword="null"/> if no row has that key.</returns>
    ValueTask<T?> FindAsync<T>(object[] keyValues, CancellationToken ct = default) where T : class;
}
