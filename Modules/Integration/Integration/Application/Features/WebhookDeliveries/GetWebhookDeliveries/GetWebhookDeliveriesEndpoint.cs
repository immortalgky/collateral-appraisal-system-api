using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliveries;

public class GetWebhookDeliveriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook-deliveries", async (
                int pageNumber,
                int pageSize,
                string? status,
                Guid? subscriptionId,
                string? eventType,
                DateTime? fromDate,
                DateTime? toDate,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetWebhookDeliveriesQuery(
                    pageNumber,
                    pageSize,
                    status,
                    subscriptionId,
                    eventType,
                    fromDate,
                    toDate);

                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetWebhookDeliveries")
            .WithTags("Admin - Webhook Deliveries")
            .Produces<PaginatedResult<WebhookDeliveryListDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("WebhookDeliveriesView");
    }
}
