using BenchmarkDotNet.Attributes;
using Flowtex.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Samples.Domain.Entities;
using Samples.Infrastructure;

namespace Samples.PerformanceDemo;

/// <summary>
/// Compares the read paths exposed by <see cref="IReadStore"/> and <see cref="IDataStore"/>
/// against a seeded in-memory SQLite database.
/// </summary>
[MemoryDiagnoser]
public class DataAccessBenchmarks
{
    private const int ProductCount = 1_000;

    private ServiceProvider _provider = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _provider = new ServiceCollection()
            .AddSampleDataAccess("Benchmarks")
            .BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await context.Database.EnsureCreatedAsync();

        var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
        await store.AddRangeAsync(Enumerable.Range(1, ProductCount).Select(i => new Product
        {
            Name = $"Product {i}",
            Description = "Benchmark product",
            Price = i,
            Stock = i % 50,
            CategoryId = 1 + i % 3,
        }));
        await store.SaveChangesAsync();
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark(Baseline = true, Description = "Query<T> (no tracking)")]
    public async Task<int> QueryNoTracking()
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReadStore>();
        return (await store.Query<Product>().ToListAsync()).Count;
    }

    [Benchmark(Description = "QueryTracked<T>")]
    public async Task<int> QueryTracked()
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
        return (await store.QueryTracked<Product>().ToListAsync()).Count;
    }

    [Benchmark(Description = "Query<T,TResult> projection")]
    public async Task<int> QueryProjection()
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReadStore>();
        return (await store.Query<Product, string>(p => p.Name).ToListAsync()).Count;
    }

    [Benchmark(Description = "ListAsync<T> with shape")]
    public async Task<int> ListAsyncShaped()
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReadStore>();
        return (await store.ListAsync<Product>(q => q.Where(p => p.Stock > 10))).Count;
    }
}
