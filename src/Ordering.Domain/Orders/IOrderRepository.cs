namespace Ordering.Domain.Orders;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Returns the customer's orders, most recently placed first.</summary>
    Task<IReadOnlyList<Order>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(Order order);
}
