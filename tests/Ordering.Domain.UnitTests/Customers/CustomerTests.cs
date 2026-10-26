using Ordering.Domain.Common;
using Ordering.Domain.Customers;

namespace Ordering.Domain.UnitTests.Customers;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Registering_a_customer_records_the_details_and_raises_CustomerRegistered()
    {
        var customer = Customer.Register("  Jane Doe ", Email.Create("jane@example.com"), Now);

        customer.Id.ShouldNotBe(Guid.Empty);
        customer.Name.ShouldBe("Jane Doe");
        customer.Email.Value.ShouldBe("jane@example.com");
        customer.RegisteredAtUtc.ShouldBe(Now);
        customer.DomainEvents.ShouldHaveSingleItem().ShouldBe(new CustomerRegistered(customer.Id, "jane@example.com", Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_customer_needs_a_name(string? name) =>
        Should.Throw<DomainException>(() => Customer.Register(name, Email.Create("jane@example.com"), Now))
            .Code.ShouldBe("customer.name.required");

    [Fact]
    public void A_name_longer_than_the_maximum_is_rejected() =>
        Should.Throw<DomainException>(() => Customer.Register(new string('x', Customer.NameMaxLength + 1), Email.Create("jane@example.com"), Now))
            .Code.ShouldBe("customer.name.too-long");
}
