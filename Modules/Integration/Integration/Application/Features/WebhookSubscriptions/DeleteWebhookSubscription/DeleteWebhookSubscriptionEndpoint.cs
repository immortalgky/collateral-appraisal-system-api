using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.DeleteWebhookSubscription;

public class DeleteWebhookSubscriptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/webhook-subscriptions/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteWebhookSubscriptionCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
