using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookDeliveries.RetryWebhookDelivery;

public class RetryWebhookDeliveryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook-deliveries/{id:guid}/retry", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new RetryWebhookDeliveryCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("RetryWebhookDelivery")
            .WithTags("Admin - Webhook Deliveries")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookDeliveriesRetry");
    }
}
