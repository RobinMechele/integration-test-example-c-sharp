using Ordering.Domain.Common;

namespace Ordering.Domain.Customers;

public sealed record CustomerRegistered(Guid CustomerId, string Email, DateTimeOffset OccurredOnUtc) : IDomainEvent;
