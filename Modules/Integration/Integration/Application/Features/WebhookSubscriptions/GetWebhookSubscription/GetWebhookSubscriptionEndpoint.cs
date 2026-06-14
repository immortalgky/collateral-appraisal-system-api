using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookSubscription;

public class GetWebhookSubscriptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook-subscriptions/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWebhookSubscriptionQuery(id), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces<WebhookSubscriptionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
