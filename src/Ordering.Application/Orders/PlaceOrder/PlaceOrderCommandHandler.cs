using Mediator;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.PlaceOrder;

public sealed class PlaceOrderCommandHandler(
    ICustomerRepository customers,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PlaceOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        if (!await customers.ExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", command.CustomerId);
        }

        var order = Order.Place(
            command.CustomerId,
            [.. command.Lines.Select(l => new NewOrderLine(l.Sku, l.Quantity, l.UnitPrice))],
            timeProvider.GetUtcNow());

        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}
