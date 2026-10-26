using FluentValidation;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.CancelOrder;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(Order.CancellationReasonMaxLength);
    }
}
