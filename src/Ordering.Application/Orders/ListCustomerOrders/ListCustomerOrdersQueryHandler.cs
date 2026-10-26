using Mediator;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.ListCustomerOrders;

public sealed class ListCustomerOrdersQueryHandler(ICustomerRepository customers, IOrderRepository orders)
    : IQueryHandler<ListCustomerOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    public async ValueTask<IReadOnlyList<OrderSummaryResponse>> Handle(ListCustomerOrdersQuery query, CancellationToken cancellationToken)
    {
        if (!await customers.ExistsAsync(query.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", query.CustomerId);
        }

        var customerOrders = await orders.ListForCustomerAsync(query.CustomerId, cancellationToken);
        return [.. customerOrders.Select(o => new OrderSummaryResponse(o.Id, o.Status.ToString(), o.Total, o.PlacedAtUtc))];
    }
}
