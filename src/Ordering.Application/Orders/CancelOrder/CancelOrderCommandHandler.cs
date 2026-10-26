using Mediator;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CancelOrderCommand>
{
    public async ValueTask<Unit> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.Cancel(command.Reason, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
