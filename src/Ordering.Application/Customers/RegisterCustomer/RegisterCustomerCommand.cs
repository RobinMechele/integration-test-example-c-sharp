using Mediator;

namespace Ordering.Application.Customers.RegisterCustomer;

/// <summary>Registers a new customer and returns its id.</summary>
public sealed record RegisterCustomerCommand(string Name, string Email) : ICommand<Guid>;
