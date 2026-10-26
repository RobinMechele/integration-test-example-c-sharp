namespace Ordering.Application.Customers;

public sealed record CustomerResponse(Guid Id, string Name, string Email, DateTimeOffset RegisteredAtUtc);
