namespace Ordering.Domain.Customers;

/// <summary>
/// Collection-like access to <see cref="Customer"/> aggregates. Implemented with EF Core in Ordering.Infrastructure.
/// Changes are committed through Ordering.Application.Abstractions.IUnitOfWork.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<bool> IsEmailTakenAsync(Email email, CancellationToken cancellationToken);

    void Add(Customer customer);
}
