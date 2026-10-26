using Mediator;

namespace Ordering.Application.Orders.ListCustomerOrders;

public sealed record ListCustomerOrdersQuery(Guid CustomerId) : IQuery<IReadOnlyList<OrderSummaryResponse>>;
