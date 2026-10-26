namespace Ordering.Domain.Common;

/// <summary>
/// Thrown when a business rule is violated. <see cref="Code"/> is a stable, machine-readable
/// identifier that the API returns to clients and that tests assert on (messages may change, codes should not).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
