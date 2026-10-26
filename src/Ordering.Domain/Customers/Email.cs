using Ordering.Domain.Common;

namespace Ordering.Domain.Customers;

/// <summary>
/// Value object for an e-mail address. Addresses are normalised (trimmed, lower-cased) so that
/// "Jane@Example.com" and "jane@example.com" are the same customer. This matters for Oracle in particular:
/// string comparisons there are case-sensitive by default, so normalising in the domain keeps the
/// unique index on EMAIL meaningful.
/// </summary>
public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainException("customer.email.required", "An e-mail address is required.");
        }

        if (normalized.Length > MaxLength)
        {
            throw new DomainException("customer.email.too-long", $"An e-mail address can be at most {MaxLength} characters.");
        }

        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        var isValid = at > 0
            && at == normalized.LastIndexOf('@')
            && at < normalized.Length - 1
            && !normalized.Contains(' ', StringComparison.Ordinal);

        if (!isValid)
        {
            throw new DomainException("customer.email.invalid", $"'{value}' is not a valid e-mail address.");
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
