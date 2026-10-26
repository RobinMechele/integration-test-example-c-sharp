using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal Total,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    IReadOnlyList<OrderLineResponse> Lines)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString(),
        order.Total,
        order.PlacedAtUtc,
        order.CancelledAtUtc,
        order.CancellationReason,
        [.. order.Lines.Select(l => new OrderLineResponse(l.Sku, l.Quantity, l.UnitPrice, l.LineTotal))]);
}

public sealed record OrderLineResponse(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderSummaryResponse(Guid Id, string Status, decimal Total, DateTimeOffset PlacedAtUtc);
