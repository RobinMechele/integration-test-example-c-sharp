using Mediator;

namespace Ordering.Application.Orders.PlaceOrder;

/// <summary>Places an order for an existing customer and returns the new order's id.</summary>
public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderLine> Lines) : ICommand<Guid>;

public sealed record PlaceOrderLine(string Sku, int Quantity, decimal UnitPrice);
