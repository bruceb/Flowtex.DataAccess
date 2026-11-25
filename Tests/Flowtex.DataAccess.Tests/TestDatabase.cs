using Flowtex.DataAccess.Abstractions;
using Flowtex.DataAccess.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Flowtex.DataAccess.Tests;

public sealed class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Stock { get; set; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

public sealed class TestDataStore(TestDbContext context) : DataStoreBase
{
    protected override DbContext Context => context;
}

/// <summary>Stands in for a transient database failure such as a deadlock or dropped connection.</summary>
public sealed class TransientTestException() : Exception("Simulated transient failure.");

/// <summary>
/// A retrying execution strategy, like SQL Server's <c>EnableRetryOnFailure</c>, that retries
/// <see cref="TransientTestException"/> with no delay.
/// </summary>
public sealed class RetryOnTestExceptionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;
}

/// <summary>
/// A private in-memory SQLite database per test. SQLite (unlike the EF InMemory provider)
/// supports real transactions and ExecuteUpdate/ExecuteDelete, so it exercises the full API.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<TestDbContext> _contexts = [];

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A context on the shared connection; disposed with the database.</summary>
    public TestDbContext CreateContext(bool retryOnFailure = false)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection, sqlite =>
            {
                if (retryOnFailure) sqlite.ExecutionStrategy(d => new RetryOnTestExceptionStrategy(d));
            })
            .Options;
        var context = new TestDbContext(options);
        _contexts.Add(context);
        return context;
    }

    /// <summary>A store over a fresh context, i.e. a new unit of work.</summary>
    public IDataStore CreateStore(bool retryOnFailure = false) => new TestDataStore(CreateContext(retryOnFailure));

    /// <summary>Reads the table through a separate context, bypassing any change tracker state.</summary>
    public List<Widget> ReadAll() => CreateContext().Widgets.AsNoTracking().OrderBy(w => w.Id).ToList();

    public void Seed(params Widget[] widgets)
    {
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options);
        context.Widgets.AddRange(widgets);
        context.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var context in _contexts) context.Dispose();
        _connection.Dispose();
    }
}
