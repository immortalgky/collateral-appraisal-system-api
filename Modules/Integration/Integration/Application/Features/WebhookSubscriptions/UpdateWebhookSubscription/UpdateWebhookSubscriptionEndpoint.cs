using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.UpdateWebhookSubscription;

public record UpdateWebhookSubscriptionRequest(string CallbackUrl, string? SecretKey);

public class UpdateWebhookSubscriptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/webhook-subscriptions/{id:guid}", async (
                Guid id,
                UpdateWebhookSubscriptionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateWebhookSubscriptionCommand(id, request.CallbackUrl, request.SecretKey);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpdateWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
