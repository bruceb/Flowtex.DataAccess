using System.Linq.Expressions;


namespace Flowtex.DataAccess.Application.Abstractions;

public interface IUpdateBuilder<T>
{
    // column = expression based on the row (server-side)
    IUpdateBuilder<T> Set<TProp>(
        Expression<Func<T, TProp>> property,
        Expression<Func<T, TProp>> value);

    // column = constant (server-side)
    IUpdateBuilder<T> SetConst<TProp>(
        Expression<Func<T, TProp>> property,
        TProp value);
}
