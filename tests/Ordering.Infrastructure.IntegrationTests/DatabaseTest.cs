using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.IntegrationTests;

/// <summary>
/// Base class for tests that use the database.
/// <list type="bullet">
///   <item>The database is wiped at the start of each test (xUnit creates a new instance per test and calls InitializeAsync).</item>
///   <item>Arrange, Act and Assert each use their own DI scope, so their own DbContext. Sharing one DbContext would let
///   EF Core's change tracker answer queries from memory and hide mapping bugs (Khorikov, chapter 10).</item>
/// </list>
/// </summary>
public abstract class DatabaseTest(InfrastructureFixture fixture) : IAsyncLifetime
{
    protected InfrastructureFixture Fixture { get; } = fixture;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await Fixture.Database.ResetAsync();
        Fixture.MessagePublisher.Reset();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Runs <paramref name="action"/> in a new scope, like one HTTP request in production.</summary>
    protected async Task<T> InNewScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected async Task InNewScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>Reads straight from the database with a fresh, non-tracking DbContext.</summary>
    protected async Task<T> QueryAsync<T>(Func<OrderingDbContext, Task<T>> query)
    {
        await using var dbContext = Fixture.Database.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await query(dbContext);
    }
}
