using Ordering.Domain.Common;

namespace Ordering.Domain.Customers;

public sealed class Customer : AggregateRoot
{
    public const int NameMaxLength = 200;

    // Required by EF Core to materialise the entity; not usable from domain code.
    private Customer()
    {
    }

    public string Name { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    /// <summary>
    /// Registers a new customer. The current time is passed in as a value instead of being read from a clock,
    /// which keeps the domain model deterministic and trivially unit-testable.
    /// </summary>
    public static Customer Register(string? name, Email email, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(email);

        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new DomainException("customer.name.required", "A customer name is required.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException("customer.name.too-long", $"A customer name can be at most {NameMaxLength} characters.");
        }

        var customer = new Customer
        {
            Id = Guid.CreateVersion7(utcNow),
            Name = trimmedName,
            Email = email,
            RegisteredAtUtc = utcNow,
        };

        customer.Raise(new CustomerRegistered(customer.Id, email.Value, utcNow));
        return customer;
    }
}
