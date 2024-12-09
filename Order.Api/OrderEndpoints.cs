using Microsoft.AspNetCore.Mvc;
using MiniValidation;
using Order.Core.Messaging;
using Order.Core.Orders;
using Polly.Wrap;

namespace Order.Api;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapPost("/order", async (
            [FromBody] Order.Core.Orders.Order order,
            [FromServices] IOrderRepository orderRepository,
            [FromServices] ISignal signal,
            [FromServices] AsyncPolicyWrap signalPolicy,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            if (!MiniValidator.TryValidate(order, out var errors))
            {
                var errorDetails = string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"));
                logger.LogWarning("Validation failed for Order: {ValidationErrors}", errorDetails);
                return Results.BadRequest(new { error = "Validation failed.", details = errors });
            }
            
            logger.LogInformation("Received order: {@Order}", order);
            var orderId = await orderRepository.Add(order, cancellationToken);
            logger.LogInformation("Order successfully saved with ID: {OrderId}", orderId);

            await signalPolicy.ExecuteAsync(() =>
            {
                signal.Make();
                return Task.CompletedTask;
            });

            return Results.Ok(new { Message = "Order receipt.", OrderId = orderId });
        });
    }
}