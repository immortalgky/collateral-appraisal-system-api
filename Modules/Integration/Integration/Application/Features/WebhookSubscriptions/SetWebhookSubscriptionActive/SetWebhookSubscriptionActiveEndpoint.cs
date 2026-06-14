using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.SetWebhookSubscriptionActive;

public class SetWebhookSubscriptionActiveEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook-subscriptions/{id:guid}/activate", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetWebhookSubscriptionActiveCommand(id, true), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ActivateWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookSubscriptionsManage");

        app.MapPost("/webhook-subscriptions/{id:guid}/deactivate", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetWebhookSubscriptionActiveCommand(id, false), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
