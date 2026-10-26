using Mediator;

namespace Ordering.Application.Orders.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;
