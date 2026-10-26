using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Outbox;

/// <summary>
/// End-to-end: an HTTP request changes data, and the domain event reaches the message broker via the outbox.
/// The broker is the system boundary, so this is where the spy sits.
/// </summary>
public sealed class OutboxFlowTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Placing_an_order_publishes_an_OrderPlaced_message_via_the_outbox()
    {
        var customerId = await RegisterCustomerAsync();
        var orderId = await PlaceOrderAsync(customerId, Line("BOOK-001", 2, 19.99m));

        // Nothing is sent to the broker while handling the request...
        Factory.MessagePublisher.Published.ShouldBeEmpty();

        // ...only when the outbox is processed.
        await RunOutboxProcessorAsync();

        Factory.MessagePublisher.Published.Select(m => m.MessageType).ShouldBe(["CustomerRegistered", "OrderPlaced"]);
        var orderPlaced = Factory.MessagePublisher.Published.Single(m => m.MessageType == "OrderPlaced");
        using var payload = JsonDocument.Parse(orderPlaced.Payload);
        payload.RootElement.GetProperty("orderId").GetGuid().ShouldBe(orderId);
        payload.RootElement.GetProperty("customerId").GetGuid().ShouldBe(customerId);
        payload.RootElement.GetProperty("total").GetDecimal().ShouldBe(39.98m);
    }

    [Fact]
    public async Task A_rejected_request_does_not_publish_anything()
    {
        var customerId = await RegisterCustomerAsync();
        var orderId = await PlaceOrderAsync(customerId, Line());
        await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "First" }, CancellationToken);
        await RunOutboxProcessorAsync();
        var publishedBefore = Factory.MessagePublisher.Published.Count;

        // Rejected by the domain (already cancelled): no data change, so no event either.
        await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "Second" }, CancellationToken);
        await RunOutboxProcessorAsync();

        Factory.MessagePublisher.Published.Count.ShouldBe(publishedBefore);
    }

    [Fact]
    public async Task The_background_service_publishes_outbox_messages_on_its_own()
    {
        // A second host with the background loop switched on. Instead of sleeping for the polling interval,
        // the fake clock is moved forward while waiting, which makes the PeriodicTimer tick immediately.
        await using var hostWithBackgroundService = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Outbox:Enabled"] = "true",
                ["Outbox:PollingInterval"] = "00:00:05",
            })));
        using var client = hostWithBackgroundService.CreateClient();

        var response = await client.PostAsJsonAsync("/customers", new { name = "Jane Doe", email = "jane@example.com" }, CancellationToken);
        response.EnsureSuccessStatusCode();

        var published = await Factory.MessagePublisher.WaitForMessageAsync(
            m => m.MessageType == "CustomerRegistered",
            timeout: TimeSpan.FromSeconds(30),
            onEachPoll: () => Factory.Time.Advance(TimeSpan.FromSeconds(5)));

        published.Payload.ShouldContain("jane@example.com");
    }
}
