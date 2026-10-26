using Ordering.Domain.Common;
using Ordering.Domain.Customers;

namespace Ordering.Domain.UnitTests.Customers;

public sealed class EmailTests
{
    [Theory]
    [InlineData("jane@example.com", "jane@example.com")]
    [InlineData("  Jane.Doe@Example.COM ", "jane.doe@example.com")]
    public void An_email_address_is_trimmed_and_lower_cased(string input, string expected) =>
        Email.Create(input).Value.ShouldBe(expected);

    [Fact]
    public void Two_addresses_that_only_differ_in_casing_are_the_same_address() =>
        Email.Create("Jane@Example.com").ShouldBe(Email.Create("jane@example.com"));

    [Theory]
    [InlineData(null, "customer.email.required")]
    [InlineData("", "customer.email.required")]
    [InlineData("   ", "customer.email.required")]
    [InlineData("jane.example.com", "customer.email.invalid")]
    [InlineData("@example.com", "customer.email.invalid")]
    [InlineData("jane@", "customer.email.invalid")]
    [InlineData("jane@@example.com", "customer.email.invalid")]
    [InlineData("jane doe@example.com", "customer.email.invalid")]
    public void An_invalid_email_address_is_rejected(string? input, string expectedCode) =>
        Should.Throw<DomainException>(() => Email.Create(input)).Code.ShouldBe(expectedCode);

    [Fact]
    public void An_email_address_longer_than_the_maximum_is_rejected()
    {
        var tooLong = new string('a', Email.MaxLength - "@example.com".Length + 1) + "@example.com";

        Should.Throw<DomainException>(() => Email.Create(tooLong)).Code.ShouldBe("customer.email.too-long");
    }
}
