using Carter;
using Integration.Domain.WebhookSubscriptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookEventTypes;

public class GetWebhookEventTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook-subscriptions/event-types", () => Results.Ok(WebhookEventTypes.All))
            .WithName("GetWebhookEventTypes")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
