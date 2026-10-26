using Mediator;
using Ordering.Application.Customers;
using Ordering.Application.Customers.GetCustomer;
using Ordering.Application.Customers.RegisterCustomer;
using Ordering.Application.Orders;
using Ordering.Application.Orders.ListCustomerOrders;

namespace Ordering.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/customers").WithTags("Customers");

        customers.MapPost("/", async (RegisterCustomerRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var customerId = await sender.Send(new RegisterCustomerCommand(request.Name, request.Email), cancellationToken);
                return TypedResults.Created($"/customers/{customerId}", new CreatedResponse(customerId));
            })
            .WithName("RegisterCustomer")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        customers.MapGet("/{customerId:guid}", async (Guid customerId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetCustomerQuery(customerId), cancellationToken)))
            .WithName("GetCustomer")
            .Produces<CustomerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        customers.MapGet("/{customerId:guid}/orders", async (Guid customerId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new ListCustomerOrdersQuery(customerId), cancellationToken)))
            .WithName("ListCustomerOrders")
            .Produces<IReadOnlyList<OrderSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record RegisterCustomerRequest(string Name, string Email);

public sealed record CreatedResponse(Guid Id);
