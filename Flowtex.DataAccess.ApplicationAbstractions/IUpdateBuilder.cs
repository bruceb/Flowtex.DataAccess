using System.Linq.Expressions;

namespace Flowtex.DataAccess.Abstractions;

/// <summary>
/// Declares the columns written by <see cref="IDataStore.ExecuteUpdateAsync{T}"/>. Every assignment
/// is translated into the single server-side <c>UPDATE</c> statement.
/// </summary>
/// <typeparam name="T">The entity type being updated.</typeparam>
public interface IUpdateBuilder<T>
{
    /// <summary>
    /// Sets a column to a value computed from the row itself, for example
    /// <c>Set(p => p.Stock, p => p.Stock - 1)</c>.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">The property (column) to set.</param>
    /// <param name="value">An expression over the current row, translated to SQL.</param>
    /// <returns>This builder, for chaining.</returns>
    IUpdateBuilder<T> Set<TProp>(
        Expression<Func<T, TProp>> property,
        Expression<Func<T, TProp>> value);

    /// <summary>
    /// Sets a column to a constant value, for example <c>SetConst(p => p.Stock, 0)</c>.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">The property (column) to set.</param>
    /// <param name="value">The value to write; sent as a parameter.</param>
    /// <returns>This builder, for chaining.</returns>
    IUpdateBuilder<T> SetConst<TProp>(
        Expression<Func<T, TProp>> property,
        TProp value);
}
