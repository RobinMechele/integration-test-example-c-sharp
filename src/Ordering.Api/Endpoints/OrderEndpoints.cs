using Mediator;
using Ordering.Application.Orders;
using Ordering.Application.Orders.CancelOrder;
using Ordering.Application.Orders.GetOrder;
using Ordering.Application.Orders.PlaceOrder;

namespace Ordering.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", async (PlaceOrderRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new PlaceOrderCommand(
                    request.CustomerId,
                    [.. (request.Lines ?? []).Select(l => new PlaceOrderLine(l.Sku, l.Quantity, l.UnitPrice))]);

                var orderId = await sender.Send(command, cancellationToken);
                return TypedResults.Created($"/orders/{orderId}", new CreatedResponse(orderId));
            })
            .WithName("PlaceOrder")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapGet("/{orderId:guid}", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetOrderQuery(orderId), cancellationToken)))
            .WithName("GetOrder")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        orders.MapPost("/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new CancelOrderCommand(orderId, request.Reason), cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("CancelOrder")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

public sealed record PlaceOrderRequest(Guid CustomerId, IReadOnlyList<PlaceOrderLineRequest>? Lines);

public sealed record PlaceOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);

public sealed record CancelOrderRequest(string Reason);
