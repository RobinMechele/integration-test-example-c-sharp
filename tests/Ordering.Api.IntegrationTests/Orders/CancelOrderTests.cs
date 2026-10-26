using System.Net;
using System.Net.Http.Json;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Orders;

public sealed class CancelOrderTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Cancelling_an_order_returns_204_and_the_order_shows_as_cancelled()
    {
        var orderId = await PlaceOrderAsync(await RegisterCustomerAsync(), Line());

        var response = await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "Ordered by mistake" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var order = await Client.GetFromJsonAsync<OrderContract>($"/orders/{orderId}", CancellationToken);
        order!.Status.ShouldBe("Cancelled");
        order.CancellationReason.ShouldBe("Ordered by mistake");
        order.CancelledAtUtc.ShouldBe(Factory.Time.GetUtcNow());
    }

    [Fact]
    public async Task Cancelling_an_order_twice_returns_409()
    {
        var orderId = await PlaceOrderAsync(await RegisterCustomerAsync(), Line());
        await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "First" }, CancellationToken);

        var response = await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "Second" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken))!.Code.ShouldBe("order.already-cancelled");
    }

    [Fact]
    public async Task Cancelling_after_the_cancellation_window_returns_409()
    {
        var orderId = await PlaceOrderAsync(await RegisterCustomerAsync(), Line());

        // No waiting for 14 days: the fake clock is moved forward.
        Factory.Time.Advance(TimeSpan.FromDays(14) + TimeSpan.FromMinutes(1));
        var response = await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "Too late" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken))!.Code.ShouldBe("order.cancellation-window-expired");
    }

    [Fact]
    public async Task Cancelling_an_unknown_order_returns_404()
    {
        var response = await Client.PostAsJsonAsync($"/orders/{Guid.NewGuid()}/cancel", new { reason = "Whatever" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cancelling_without_a_reason_returns_400()
    {
        var orderId = await PlaceOrderAsync(await RegisterCustomerAsync(), Line());

        var response = await Client.PostAsJsonAsync($"/orders/{orderId}/cancel", new { reason = "" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken))!.Errors.ShouldNotBeNull().Keys.ShouldBe(["reason"]);
    }
}
