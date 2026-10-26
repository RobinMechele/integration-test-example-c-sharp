using FluentValidation.TestHelper;
using Ordering.Application.Customers.RegisterCustomer;
using Ordering.Domain.Customers;

namespace Ordering.Application.UnitTests.Validators;

public sealed class RegisterCustomerCommandValidatorTests
{
    private readonly RegisterCustomerCommandValidator _validator = new();

    [Fact]
    public void A_complete_registration_is_valid() =>
        _validator.TestValidate(new RegisterCustomerCommand("Jane Doe", "jane@example.com")).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void The_name_is_required(string name) =>
        _validator.TestValidate(new RegisterCustomerCommand(name, "jane@example.com")).ShouldHaveValidationErrorFor(c => c.Name);

    [Fact]
    public void The_name_has_a_maximum_length() =>
        _validator.TestValidate(new RegisterCustomerCommand(new string('x', Customer.NameMaxLength + 1), "jane@example.com"))
            .ShouldHaveValidationErrorFor(c => c.Name);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void The_email_must_be_an_email_address(string email) =>
        _validator.TestValidate(new RegisterCustomerCommand("Jane Doe", email)).ShouldHaveValidationErrorFor(c => c.Email);
}
