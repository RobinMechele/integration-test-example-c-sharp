using FluentValidation;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.PlaceOrder;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Lines).NotEmpty().Must(lines => lines.Count <= Order.MaxLines)
            .WithMessage($"An order can contain at most {Order.MaxLines} lines.");
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Sku).NotEmpty().MaximumLength(OrderLine.SkuMaxLength);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, Order.MaxQuantityPerLine);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });
    }
}
