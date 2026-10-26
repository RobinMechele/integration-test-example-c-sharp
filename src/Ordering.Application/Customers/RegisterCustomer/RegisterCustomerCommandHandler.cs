using Mediator;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;

namespace Ordering.Application.Customers.RegisterCustomer;

public sealed class RegisterCustomerCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RegisterCustomerCommand, Guid>
{
    public async ValueTask<Guid> Handle(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);

        // Fast, friendly check. The unique index on CUSTOMERS.EMAIL is what really guarantees uniqueness
        // when two requests race; IUnitOfWork turns that violation into the same ConflictException.
        if (await customers.IsEmailTakenAsync(email, cancellationToken))
        {
            throw new ConflictException("customer.email.taken", $"A customer with e-mail '{email}' already exists.");
        }

        var customer = Customer.Register(command.Name, email, timeProvider.GetUtcNow());
        customers.Add(customer);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }
}
