using Flowtex.DataAccess.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.Infrastructure;

/// <summary>
/// Registers the sample data store against a shared, in-memory SQLite database.
/// </summary>
/// <remarks>
/// SQLite is used rather than the EF Core InMemory provider because InMemory does not support
/// transactions (<c>InTransactionAsync</c>) or set-based <c>ExecuteUpdateAsync</c>/<c>ExecuteDeleteAsync</c>.
/// A shared-cache in-memory database lives only while at least one connection to it is open, so a
/// keep-alive connection is opened here and held by the container for the application's lifetime.
/// Each DbContext still opens its own connection, which keeps concurrent web requests safe.
/// </remarks>
public static class SampleServiceCollectionExtensions
{
    public static IServiceCollection AddSampleDataAccess(this IServiceCollection services, string databaseName = "Samples")
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseName,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        services.AddSingleton(keepAlive);

        services.AddDbContext<SampleDbContext>(options => options.UseSqlite(connectionString));

        // IReadStore resolves to the same scoped instance as IDataStore.
        services.AddScoped<IDataStore, SampleDataStore>();
        services.AddScoped<IReadStore>(provider => provider.GetRequiredService<IDataStore>());

        return services;
    }
}
