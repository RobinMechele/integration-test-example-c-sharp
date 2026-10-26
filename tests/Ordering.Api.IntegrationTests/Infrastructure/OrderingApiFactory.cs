using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Ordering.Infrastructure.Messaging;
using Ordering.TestKit;

namespace Ordering.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real application in memory (TestServer) against a real Oracle database in a container.
/// Replaced on purpose, and nothing else:
/// <list type="bullet">
///   <item>the message broker (unmanaged dependency) -> <see cref="MessagePublisherSpy"/>;</item>
///   <item>the clock -> <see cref="FakeTimeProvider"/>, so time-based rules can be tested without waiting;</item>
///   <item>the outbox background loop is switched off; tests run the outbox processor explicitly.</item>
/// </list>
/// </summary>
public sealed class OrderingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly OracleTestDatabase _database = new();

    public MessagePublisherSpy MessagePublisher { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    public OracleTestDatabase Database => _database;

    public async ValueTask InitializeAsync() => await _database.InitializeAsync();

    /// <summary>Called before every test: empty database, empty spy.</summary>
    public async Task ResetAsync()
    {
        await _database.ResetAsync();
        MessagePublisher.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Ordering"] = _database.ConnectionString,
            ["Database:MigrateOnStartup"] = "false", // the test database is migrated once by OracleTestDatabase
            ["Outbox:Enabled"] = "false",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(MessagePublisher);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
