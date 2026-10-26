using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliverySummary;

public class GetWebhookDeliverySummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook-deliveries/summary", async (
                Guid? subscriptionId,
                string? systemCode,
                string? eventType,
                string? search,
                DateTime? fromDate,
                DateTime? toDate,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetWebhookDeliverySummaryQuery(subscriptionId, systemCode, eventType, search, fromDate, toDate),
                    cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetWebhookDeliverySummary")
            .WithTags("Admin - Webhook Deliveries")
            .Produces<WebhookDeliverySummaryDto>(StatusCodes.Status200OK)
            .RequireAuthorization("WebhookDeliveriesView");
    }
}
