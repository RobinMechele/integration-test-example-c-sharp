using FluentValidation.TestHelper;
using Ordering.Application.Orders.CancelOrder;
using Ordering.Domain.Orders;

namespace Ordering.Application.UnitTests.Validators;

public sealed class CancelOrderCommandValidatorTests
{
    private readonly CancelOrderCommandValidator _validator = new();

    [Fact]
    public void A_cancellation_with_a_reason_is_valid() =>
        _validator.TestValidate(new CancelOrderCommand(Guid.NewGuid(), "Changed my mind")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void The_reason_is_required() =>
        _validator.TestValidate(new CancelOrderCommand(Guid.NewGuid(), "")).ShouldHaveValidationErrorFor(c => c.Reason);

    [Fact]
    public void The_reason_has_a_maximum_length() =>
        _validator.TestValidate(new CancelOrderCommand(Guid.NewGuid(), new string('x', Order.CancellationReasonMaxLength + 1)))
            .ShouldHaveValidationErrorFor(c => c.Reason);
}
