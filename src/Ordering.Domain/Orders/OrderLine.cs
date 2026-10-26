namespace Ordering.Domain.Orders;

/// <summary>A line of an <see cref="Order"/>. Only the Order aggregate can create lines.</summary>
public sealed class OrderLine
{
    public const int SkuMaxLength = 32;

    // Required by EF Core.
    private OrderLine()
    {
    }

    internal OrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
