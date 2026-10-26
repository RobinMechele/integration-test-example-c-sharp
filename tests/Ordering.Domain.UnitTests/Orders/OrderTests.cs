using Ordering.Domain.Common;
using Ordering.Domain.Orders;

namespace Ordering.Domain.UnitTests.Orders;

/// <summary>
/// Business rules are tested here, as unit tests: they are fast, need no infrastructure and can cover every
/// edge case. The integration tests then only need the happy path plus what unit tests cannot reach.
/// </summary>
public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Placing_an_order_calculates_the_total_and_raises_OrderPlaced()
    {
        var order = Order.Place(CustomerId, [new NewOrderLine("BOOK-001", 2, 19.99m), new NewOrderLine("PEN-042", 1, 2.50m)], Now);

        order.Status.ShouldBe(OrderStatus.Placed);
        order.CustomerId.ShouldBe(CustomerId);
        order.PlacedAtUtc.ShouldBe(Now);
        order.Total.ShouldBe(42.48m);
        order.Version.ShouldBe(1);
        order.DomainEvents.ShouldHaveSingleItem().ShouldBe(new OrderPlaced(order.Id, CustomerId, 42.48m, Now));
    }

    [Fact]
    public void Lines_for_the_same_sku_are_merged_regardless_of_casing_and_whitespace()
    {
        var order = Order.Place(CustomerId, [new NewOrderLine("book-001", 1, 10m), new NewOrderLine(" BOOK-001 ", 2, 10m)], Now);

        var line = order.Lines.ShouldHaveSingleItem();
        line.Sku.ShouldBe("BOOK-001");
        line.Quantity.ShouldBe(3);
        order.Total.ShouldBe(30m);
    }

    [Fact]
    public void The_same_sku_with_different_prices_is_rejected() =>
        ShouldViolate("order.line.conflicting-prices", () =>
            Order.Place(CustomerId, [new NewOrderLine("BOOK-001", 1, 10m), new NewOrderLine("BOOK-001", 1, 12m)], Now));

    [Fact]
    public void An_order_needs_at_least_one_line() =>
        ShouldViolate("order.lines.empty", () => Order.Place(CustomerId, [], Now));

    [Fact]
    public void An_order_needs_a_customer() =>
        ShouldViolate("order.customer.required", () => Order.Place(Guid.Empty, [new NewOrderLine("A", 1, 1m)], Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_line_needs_a_positive_quantity(int quantity) =>
        ShouldViolate("order.line.quantity.invalid", () => Order.Place(CustomerId, [new NewOrderLine("A", quantity, 1m)], Now));

    [Fact]
    public void The_merged_quantity_cannot_exceed_the_maximum_per_line() =>
        ShouldViolate("order.line.quantity-too-large", () => Order.Place(
            CustomerId,
            [new NewOrderLine("A", Order.MaxQuantityPerLine, 1m), new NewOrderLine("a", 1, 1m)],
            Now));

    [Fact]
    public void A_single_line_cannot_exceed_the_maximum_quantity() =>
        ShouldViolate("order.line.quantity-too-large", () => Order.Place(CustomerId, [new NewOrderLine("A", Order.MaxQuantityPerLine + 1, 1m)], Now));

    [Fact]
    public void Huge_quantities_cannot_overflow_past_the_maximum() =>
        ShouldViolate("order.line.quantity-too-large", () => Order.Place(
            CustomerId,
            [new NewOrderLine("A", int.MaxValue, 1m), new NewOrderLine("A", int.MaxValue, 1m)],
            Now));

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    public void A_line_needs_a_positive_unit_price(string unitPrice) =>
        ShouldViolate("order.line.unit-price.invalid", () => Order.Place(CustomerId, [new NewOrderLine("A", 1, decimal.Parse(unitPrice, System.Globalization.CultureInfo.InvariantCulture))], Now));

    [Fact]
    public void A_unit_price_with_more_than_two_decimals_is_rejected() =>
        ShouldViolate("order.line.unit-price.precision", () => Order.Place(CustomerId, [new NewOrderLine("A", 1, 19.999m)], Now));

    [Fact]
    public void A_line_needs_a_sku() =>
        ShouldViolate("order.line.sku.required", () => Order.Place(CustomerId, [new NewOrderLine(" ", 1, 1m)], Now));

    [Fact]
    public void An_order_cannot_contain_more_than_the_maximum_number_of_products()
    {
        var lines = Enumerable.Range(0, Order.MaxLines + 1).Select(i => new NewOrderLine($"SKU-{i}", 1, 1m)).ToList();

        ShouldViolate("order.lines.too-many", () => Order.Place(CustomerId, lines, Now));
    }

    [Fact]
    public void Cancelling_an_order_records_why_and_when_and_raises_OrderCancelled()
    {
        var order = PlacedOrder();

        order.Cancel("  Ordered by mistake ", Now.AddHours(1));

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancelledAtUtc.ShouldBe(Now.AddHours(1));
        order.CancellationReason.ShouldBe("Ordered by mistake");
        order.Version.ShouldBe(2);
        order.DomainEvents.ShouldHaveSingleItem().ShouldBe(new OrderCancelled(order.Id, "Ordered by mistake", Now.AddHours(1)));
    }

    [Fact]
    public void An_order_can_still_be_cancelled_on_the_last_moment_of_the_cancellation_window()
    {
        var order = PlacedOrder();

        order.Cancel("Just in time", Now + Order.CancellationWindow);

        order.Status.ShouldBe(OrderStatus.Cancelled);
    }

    [Fact]
    public void An_order_cannot_be_cancelled_after_the_cancellation_window() =>
        ShouldViolate("order.cancellation-window-expired", () => PlacedOrder().Cancel("Too late", Now + Order.CancellationWindow + TimeSpan.FromTicks(1)));

    [Fact]
    public void An_order_cannot_be_cancelled_twice()
    {
        var order = PlacedOrder();
        order.Cancel("First", Now);

        ShouldViolate("order.already-cancelled", () => order.Cancel("Second", Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Cancelling_needs_a_reason(string? reason) =>
        ShouldViolate("order.cancellation-reason.required", () => PlacedOrder().Cancel(reason, Now));

    private static Order PlacedOrder()
    {
        var order = Order.Place(CustomerId, [new NewOrderLine("BOOK-001", 1, 10m)], Now);
        order.ClearDomainEvents();
        return order;
    }

    private static void ShouldViolate(string expectedCode, Action action) =>
        Should.Throw<DomainException>(action).Code.ShouldBe(expectedCode);
}
