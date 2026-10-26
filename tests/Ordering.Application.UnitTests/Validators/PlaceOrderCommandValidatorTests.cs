using FluentValidation.TestHelper;
using Ordering.Application.Orders.PlaceOrder;
using Ordering.Domain.Orders;

namespace Ordering.Application.UnitTests.Validators;

public sealed class PlaceOrderCommandValidatorTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private readonly PlaceOrderCommandValidator _validator = new();

    [Fact]
    public void A_complete_order_is_valid() =>
        _validator.TestValidate(new PlaceOrderCommand(CustomerId, [new PlaceOrderLine("BOOK-001", 2, 19.99m)]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void The_customer_is_required() =>
        _validator.TestValidate(new PlaceOrderCommand(Guid.Empty, [new PlaceOrderLine("BOOK-001", 1, 1m)]))
            .ShouldHaveValidationErrorFor(c => c.CustomerId);

    [Fact]
    public void At_least_one_line_is_required() =>
        _validator.TestValidate(new PlaceOrderCommand(CustomerId, [])).ShouldHaveValidationErrorFor(c => c.Lines);

    [Fact]
    public void The_number_of_lines_is_limited()
    {
        var lines = Enumerable.Range(0, Order.MaxLines + 1).Select(i => new PlaceOrderLine($"SKU-{i}", 1, 1m)).ToList();

        _validator.TestValidate(new PlaceOrderCommand(CustomerId, lines)).ShouldHaveValidationErrorFor(c => c.Lines);
    }

    [Theory]
    [InlineData("", 1, "1", "Lines[0].Sku")]
    [InlineData("BOOK-001", 0, "1", "Lines[0].Quantity")]
    [InlineData("BOOK-001", 1, "0", "Lines[0].UnitPrice")]
    [InlineData("BOOK-001", 1, "19.999", "Lines[0].UnitPrice")]
    public void Each_line_is_validated(string sku, int quantity, string unitPrice, string expectedProperty)
    {
        var line = new PlaceOrderLine(sku, quantity, decimal.Parse(unitPrice, System.Globalization.CultureInfo.InvariantCulture));

        _validator.TestValidate(new PlaceOrderCommand(CustomerId, [line])).ShouldHaveValidationErrorFor(expectedProperty);
    }
}
