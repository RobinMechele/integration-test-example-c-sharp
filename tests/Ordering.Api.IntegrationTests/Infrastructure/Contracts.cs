namespace Ordering.Api.IntegrationTests.Infrastructure;

// The HTTP contract as seen by a client. Deliberately NOT the production DTO types: if someone renames a
// property in the API, these tests fail instead of silently following the rename (and breaking real clients).
public sealed record CreatedResponse(Guid Id);

public sealed record CustomerContract(Guid Id, string Name, string Email, DateTimeOffset RegisteredAtUtc);

public sealed record OrderContract(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal Total,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    IReadOnlyList<OrderLineContract> Lines);

public sealed record OrderLineContract(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderSummaryContract(Guid Id, string Status, decimal Total, DateTimeOffset PlacedAtUtc);

public sealed record ProblemContract(
    int Status,
    string Title,
    string? Detail,
    string? Code,
    IReadOnlyDictionary<string, string[]>? Errors);
