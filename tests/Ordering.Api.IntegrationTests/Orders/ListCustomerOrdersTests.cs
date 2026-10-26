using System.Net;
using System.Net.Http.Json;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Orders;

public sealed class ListCustomerOrdersTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task A_customers_orders_are_listed_most_recent_first()
    {
        var customerId = await RegisterCustomerAsync();
        var otherCustomerId = await RegisterCustomerAsync(email: "someone.else@example.com");
        var first = await PlaceOrderAsync(customerId, Line(unitPrice: 1m));
        Factory.Time.Advance(TimeSpan.FromHours(1));
        var second = await PlaceOrderAsync(customerId, Line(unitPrice: 2m));
        Factory.Time.Advance(TimeSpan.FromHours(1));
        var third = await PlaceOrderAsync(customerId, Line(unitPrice: 3m));
        await PlaceOrderAsync(otherCustomerId, Line());
        await Client.PostAsJsonAsync($"/orders/{second}/cancel", new { reason = "Changed my mind" }, CancellationToken);

        var orders = await Client.GetFromJsonAsync<List<OrderSummaryContract>>($"/customers/{customerId}/orders", CancellationToken);

        orders.ShouldNotBeNull();
        orders.Select(o => (o.Id, o.Status, o.Total)).ShouldBe([(third, "Placed", 3m), (second, "Cancelled", 2m), (first, "Placed", 1m)]);
    }

    [Fact]
    public async Task Listing_the_orders_of_an_unknown_customer_returns_404()
    {
        var response = await Client.GetAsync($"/customers/{Guid.NewGuid()}/orders", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
