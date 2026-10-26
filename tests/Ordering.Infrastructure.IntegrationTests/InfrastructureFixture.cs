using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Messaging;
using Ordering.TestKit;

namespace Ordering.Infrastructure.IntegrationTests;

/// <summary>
/// Starts the Oracle container and builds the Infrastructure layer's real service registrations
/// (<see cref="DependencyInjection.AddInfrastructure"/>), replacing only the clock and the message broker.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    private ServiceProvider? _services;

    public OracleTestDatabase Database { get; } = new();

    public MessagePublisherSpy MessagePublisher { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    public IServiceProvider Services => _services ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ordering"] = Database.ConnectionString,
            })
            .Build();

        _services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddSingleton<TimeProvider>(Time)
            .AddSingleton<IMessagePublisher>(MessagePublisher)
            .AddInfrastructure()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await Database.DisposeAsync();
    }
}
