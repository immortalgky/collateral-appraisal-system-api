using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

public class GetPendingInternalSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-internal/summary",
                async (
                    string[]? slaStatus,
                    string? search,
                    string[]? activityId,
                    string[]? slaBucket,
                    string? pic,
                    string? picType,
                    string[]? purpose,
                    string[]? propertyType,
                    string[]? taskType,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingInternalFilter(slaStatus, search, activityId, null, null, slaBucket, pic, picType, purpose, propertyType, taskType);
                    var result = await sender.Send(new GetPendingInternalSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingInternalSummary")
            .Produces<MonitoringSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Internal summary KPIs")
            .WithDescription("Returns Total/Breached/AtRisk/Healthy counts for the Pending Internal tab under the same filter as the list endpoint. Requires any MONITORING:PENDING_INTERNAL:* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingInternal);
    }
}
