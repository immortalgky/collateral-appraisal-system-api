using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

public class GetPendingExternalSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-external/summary",
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
                    string[]? appraisalCompanyId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingExternalFilter(slaStatus, search, activityId, null, null, slaBucket, pic, picType, purpose, propertyType, taskType, appraisalCompanyId);
                    var result = await sender.Send(new GetPendingExternalSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingExternalSummary")
            .Produces<MonitoringSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending External summary KPIs")
            .WithDescription("Returns Total/Breached/AtRisk/Healthy counts for the Pending External tab under the same filter as the list endpoint. Requires any MONITORING:PENDING_EXTERNAL:* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingExternal);
    }
}
