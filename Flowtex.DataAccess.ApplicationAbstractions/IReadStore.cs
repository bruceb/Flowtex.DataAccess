using System.Linq.Expressions;

namespace Flowtex.DataAccess.Application.Abstractions;

public interface IReadStore
{
    IQueryable<T> Query<T>() where T : class;

    // Helpers
    IQueryable<TResult> Query<T, TResult>(Expression<Func<T, TResult>> selector) where T : class;
    Task<List<T>> ListAsync<T>(Func<IQueryable<T>, IQueryable<T>>? shape = null, CancellationToken ct = default) where T : class;
    Task<List<TResult>> ListAsync<T, TResult>(Func<IQueryable<T>, IQueryable<TResult>> shape, CancellationToken ct = default) where T : class;
}
