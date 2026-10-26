using Ordering.Domain.Common;

namespace Ordering.Domain.Orders;

public sealed record OrderPlaced(Guid OrderId, Guid CustomerId, decimal Total, DateTimeOffset OccurredOnUtc) : IDomainEvent;
