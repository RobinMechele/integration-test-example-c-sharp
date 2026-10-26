using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Ordering.Infrastructure.Outbox;
using Ordering.Infrastructure.Persistence;
using Respawn;
using Testcontainers.Oracle;
using Xunit;

namespace Ordering.TestKit;

/// <summary>
/// A real Oracle database for integration tests, running in a throw-away Docker container.
/// <list type="number">
///   <item>One container per test assembly (starting Oracle takes seconds, so it is shared, not per test).</item>
///   <item>The real EF Core migrations are applied once, so the tests also prove that the migrations work.</item>
///   <item><see cref="ResetAsync"/> deletes all data (but keeps the schema) before every test, using Respawn.</item>
/// </list>
/// </summary>
public sealed class OracleTestDatabase : IAsyncLifetime
{
    /// <summary>
    /// Pinned image (Testcontainers best practice: never rely on a floating "latest" tag).
    /// gvenzl/oracle-free is the community-standard Oracle Database Free image that Testcontainers documents;
    /// the "slim-faststart" variant has a pre-created database and starts in seconds.
    /// </summary>
    public const string Image = "gvenzl/oracle-free:23.26.3-slim-faststart";

    private readonly OracleContainer _container = new OracleBuilder(Image).Build();
    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The Oracle schema (= user) the application tables live in.</summary>
    public string Schema => new OracleConnectionStringBuilder(ConnectionString).UserID.ToUpperInvariant();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        await using var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Oracle,
            SchemasToInclude = [Schema],
            // Keep the record of applied migrations; only business data is wiped.
            TablesToIgnore = ["EF_MIGRATIONS_HISTORY"],
        });
    }

    /// <summary>
    /// Deletes all rows from all application tables. Called at the <i>start</i> of every test (not at the end),
    /// so each test starts from a known state even if a previous test run crashed half-way.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The database has not been initialised.");
        }

        await using var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// A DbContext configured exactly like production (same provider options and outbox interceptor).
    /// </summary>
    public OrderingDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OrderingDbContext>()
            .UseOrderingOracle(ConnectionString)
            .AddInterceptors(new InsertOutboxMessagesInterceptor())
            .Options);

    public async Task<OracleConnection> OpenConnectionAsync()
    {
        var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
