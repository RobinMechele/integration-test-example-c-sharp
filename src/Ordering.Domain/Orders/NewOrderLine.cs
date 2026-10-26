namespace Ordering.Domain.Orders;

/// <summary>Input for <see cref="Order.Place"/>: one requested product line.</summary>
public sealed record NewOrderLine(string Sku, int Quantity, decimal UnitPrice);
