using System.Linq.Expressions;

namespace Flowtex.DataAccess.Abstractions;

public interface IReadStore
{
    /// <summary>
    /// Returns a no-tracking <see cref="IQueryable{T}"/> for ad-hoc queries.
    /// The no-tracking convention is applied by the infrastructure layer.
    /// Opt into tracking explicitly via <see cref="IDataStore.QueryTracked{T}"/>.
    /// </summary>
    IQueryable<T> Query<T>() where T : class;

    // Helpers
    IQueryable<TResult> Query<T, TResult>(Expression<Func<T, TResult>> selector) where T : class;
    Task<List<T>> ListAsync<T>(Func<IQueryable<T>, IQueryable<T>>? shape = null, CancellationToken ct = default) where T : class;
    Task<List<TResult>> ListAsync<T, TResult>(Func<IQueryable<T>, IQueryable<TResult>> shape, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Looks up an entity by primary key. Uses the identity-map cache before
    /// issuing a database round-trip, making it more efficient than
    /// <see cref="Query{T}"/> for single-key lookups.
    /// </summary>
    ValueTask<T?> FindAsync<T>(object[] keyValues, CancellationToken ct = default) where T : class;
}
