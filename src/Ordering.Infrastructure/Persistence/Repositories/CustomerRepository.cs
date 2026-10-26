using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Customers;

namespace Ordering.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(OrderingDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);

    public Task<bool> IsEmailTakenAsync(Email email, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(c => c.Email == email, cancellationToken);

    public void Add(Customer customer) => dbContext.Customers.Add(customer);
}
