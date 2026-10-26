using FluentValidation;
using Ordering.Domain.Customers;

namespace Ordering.Application.Customers.RegisterCustomer;

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Customer.NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
    }
}
