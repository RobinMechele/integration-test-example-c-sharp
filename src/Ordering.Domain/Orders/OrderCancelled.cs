using Ordering.Domain.Common;

namespace Ordering.Domain.Orders;

public sealed record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset OccurredOnUtc) : IDomainEvent;
