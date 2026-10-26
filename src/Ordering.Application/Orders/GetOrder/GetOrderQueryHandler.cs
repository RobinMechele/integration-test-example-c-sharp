using Mediator;
using Ordering.Application.Exceptions;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.GetOrder;

public sealed class GetOrderQueryHandler(IOrderRepository orders) : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public async ValueTask<OrderResponse> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);

        return OrderResponse.From(order);
    }
}
