using Ordering.Domain.Common;

namespace Ordering.Domain.Orders;

public sealed class Order : AggregateRoot
{
    public const int MaxLines = 20;
    public const int MaxQuantityPerLine = 1_000;
    public const int CancellationReasonMaxLength = 500;

    /// <summary>Orders can only be cancelled during this period after they were placed.</summary>
    public static readonly TimeSpan CancellationWindow = TimeSpan.FromDays(14);

    private readonly List<OrderLine> _lines = [];

    // Required by EF Core.
    private Order()
    {
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public decimal Total { get; private set; }

    public DateTimeOffset PlacedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Incremented on every state change; EF Core adds it to the WHERE clause
    /// of UPDATE statements, so two concurrent changes to the same order cannot both succeed.
    /// </summary>
    public int Version { get; private set; }

    public static Order Place(Guid customerId, IReadOnlyCollection<NewOrderLine> lines, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (customerId == Guid.Empty)
        {
            throw new DomainException("order.customer.required", "An order must belong to a customer.");
        }

        if (lines.Count == 0)
        {
            throw new DomainException("order.lines.empty", "An order must contain at least one line.");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(utcNow),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            PlacedAtUtc = utcNow,
            Version = 1,
        };

        // Lines for the same SKU are merged into one line.
        foreach (var group in lines.GroupBy(l => NormalizeSku(l.Sku)))
        {
            var prices = group.Select(l => l.UnitPrice).Distinct().ToList();
            if (prices.Count > 1)
            {
                throw new DomainException("order.line.conflicting-prices", $"SKU '{group.Key}' appears with different unit prices.");
            }

            // Summed as long so that huge quantities cannot overflow and sneak past the maximum.
            var quantity = group.Sum(l => (long)EnsureValidQuantity(group.Key, l.Quantity));
            if (quantity > MaxQuantityPerLine)
            {
                throw new DomainException("order.line.quantity-too-large", $"At most {MaxQuantityPerLine} units of SKU '{group.Key}' can be ordered.");
            }

            order._lines.Add(new OrderLine(group.Key, (int)quantity, EnsureValidUnitPrice(group.Key, prices[0])));
        }

        if (order._lines.Count > MaxLines)
        {
            throw new DomainException("order.lines.too-many", $"An order can contain at most {MaxLines} different products.");
        }

        order.Total = order._lines.Sum(l => l.LineTotal);
        order.Raise(new OrderPlaced(order.Id, customerId, order.Total, utcNow));
        return order;
    }

    public void Cancel(string? reason, DateTimeOffset utcNow)
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new DomainException("order.already-cancelled", "The order has already been cancelled.");
        }

        if (utcNow - PlacedAtUtc > CancellationWindow)
        {
            throw new DomainException("order.cancellation-window-expired", $"Orders can only be cancelled within {CancellationWindow.TotalDays} days.");
        }

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
        {
            throw new DomainException("order.cancellation-reason.required", "A cancellation reason is required.");
        }

        if (trimmedReason.Length > CancellationReasonMaxLength)
        {
            throw new DomainException("order.cancellation-reason.too-long", $"A cancellation reason can be at most {CancellationReasonMaxLength} characters.");
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = utcNow;
        CancellationReason = trimmedReason;
        Version++;

        Raise(new OrderCancelled(Id, trimmedReason, utcNow));
    }

    private static string NormalizeSku(string? sku)
    {
        var normalized = sku?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainException("order.line.sku.required", "Every order line needs a SKU.");
        }

        if (normalized.Length > OrderLine.SkuMaxLength)
        {
            throw new DomainException("order.line.sku.too-long", $"A SKU can be at most {OrderLine.SkuMaxLength} characters.");
        }

        return normalized;
    }

    private static int EnsureValidQuantity(string sku, int quantity) =>
        quantity > 0
            ? quantity
            : throw new DomainException("order.line.quantity.invalid", $"The quantity for SKU '{sku}' must be greater than zero.");

    private static decimal EnsureValidUnitPrice(string sku, decimal unitPrice)
    {
        if (unitPrice <= 0)
        {
            throw new DomainException("order.line.unit-price.invalid", $"The unit price for SKU '{sku}' must be greater than zero.");
        }

        // Prices are stored as NUMBER(18,2). Rejecting extra decimals here avoids silent rounding by the database.
        if (decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw new DomainException("order.line.unit-price.precision", $"The unit price for SKU '{sku}' can have at most two decimals.");
        }

        return unitPrice;
    }
}
