using Mediator;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;

namespace Ordering.Application.Customers.GetCustomer;

public sealed class GetCustomerQueryHandler(ICustomerRepository customers) : IQueryHandler<GetCustomerQuery, CustomerResponse>
{
    public async ValueTask<CustomerResponse> Handle(GetCustomerQuery query, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(query.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer", query.CustomerId);

        return new CustomerResponse(customer.Id, customer.Name, customer.Email.Value, customer.RegisteredAtUtc);
    }
}
