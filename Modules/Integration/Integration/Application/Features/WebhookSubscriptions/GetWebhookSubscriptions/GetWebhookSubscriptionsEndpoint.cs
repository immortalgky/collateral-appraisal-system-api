using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookSubscriptions;

public class GetWebhookSubscriptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook-subscriptions", async (
                int? pageNumber,
                int? pageSize,
                string? systemCode,
                bool? isActive,
                string? eventType,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetWebhookSubscriptionsQuery(
                    pageNumber ?? 1, pageSize ?? 20, systemCode, isActive, eventType);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetWebhookSubscriptions")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces<PaginatedResult<WebhookSubscriptionDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
