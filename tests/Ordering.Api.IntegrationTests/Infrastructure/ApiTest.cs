using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for API tests. xUnit creates a new instance per test, and InitializeAsync resets the database
/// first, so every test starts from an empty database no matter what ran before it.
/// </summary>
public abstract class ApiTest(OrderingApiFactory factory) : IAsyncLifetime
{
    protected OrderingApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }

    // ----- Arrange helpers: they go through the public API, like a real client would. -----

    protected async Task<Guid> RegisterCustomerAsync(string name = "Jane Doe", string email = "jane.doe@example.com")
    {
        var response = await Client.PostAsJsonAsync("/customers", new { name, email }, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(CancellationToken))!.Id;
    }

    protected async Task<Guid> PlaceOrderAsync(Guid customerId, params object[] lines)
    {
        var response = await Client.PostAsJsonAsync("/orders", new { customerId, lines }, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(CancellationToken))!.Id;
    }

    protected static object Line(string sku = "BOOK-001", int quantity = 1, decimal unitPrice = 10m) =>
        new { sku, quantity, unitPrice };

    /// <summary>
    /// Runs the outbox processor once, exactly as the background service would, but deterministically.
    /// </summary>
    protected async Task<int> RunOutboxProcessorAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessAsync(CancellationToken);
    }
}
