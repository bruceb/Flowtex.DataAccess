using System.Linq.Expressions;

namespace Flowtex.DataAccess.Application.Abstractions;

public interface IDataStore : IReadStore
{
    // Reads (tracked when you need change tracking)
    IQueryable<T> QueryTracked<T>() where T : class;

    // CRUD (instance-based)
    ValueTask<ISaveHandle> AddAsync<T>(T entity, CancellationToken ct = default) where T : class;
    ValueTask<ISaveHandle> AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class;
    ISaveHandle Update<T>(T entity) where T : class;
    ISaveHandle Remove<T>(T entity) where T : class;

    Task<int> ExecuteDeleteAsync<T>(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default) where T : class;

    Task<int> ExecuteUpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Action<IUpdateBuilder<T>> build,
        CancellationToken ct = default) where T : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> InTransactionAsync<TResult>(Func<IDataStore, Task<TResult>> work, CancellationToken ct = default);
}