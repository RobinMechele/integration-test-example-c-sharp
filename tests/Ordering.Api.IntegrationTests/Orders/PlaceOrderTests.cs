using System.Net;
using System.Net.Http.Json;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Orders;

public sealed class PlaceOrderTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Placing_an_order_returns_201_and_the_order_can_be_read_back()
    {
        var customerId = await RegisterCustomerAsync();

        var response = await Client.PostAsJsonAsync(
            "/orders",
            new { customerId, lines = new[] { Line("BOOK-001", 2, 19.99m), Line("PEN-042", 1, 2.50m) } },
            CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await Client.GetFromJsonAsync<OrderContract>(response.Headers.Location, CancellationToken);
        order.ShouldNotBeNull();
        order.CustomerId.ShouldBe(customerId);
        order.Status.ShouldBe("Placed");
        order.Total.ShouldBe(42.48m);
        order.PlacedAtUtc.ShouldBe(Factory.Time.GetUtcNow());
        order.CancelledAtUtc.ShouldBeNull();
        order.Lines.ShouldBe(
            [new OrderLineContract("BOOK-001", 2, 19.99m, 39.98m), new OrderLineContract("PEN-042", 1, 2.50m, 2.50m)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Placing_an_order_for_an_unknown_customer_returns_404()
    {
        var response = await Client.PostAsJsonAsync("/orders", new { customerId = Guid.NewGuid(), lines = new[] { Line() } }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Placing_an_order_without_lines_returns_400()
    {
        var customerId = await RegisterCustomerAsync();

        var response = await Client.PostAsJsonAsync("/orders", new { customerId, lines = Array.Empty<object>() }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken);
        problem!.Errors.ShouldNotBeNull().Keys.ShouldBe(["lines"]);
    }

    [Fact]
    public async Task A_price_with_more_than_two_decimals_is_rejected_instead_of_being_rounded_by_the_database()
    {
        var customerId = await RegisterCustomerAsync();

        var response = await Client.PostAsJsonAsync("/orders", new { customerId, lines = new[] { Line(unitPrice: 19.999m) } }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken);
        problem!.Errors.ShouldNotBeNull().Keys.ShouldBe(["lines[0].unitPrice"]);
    }
}
