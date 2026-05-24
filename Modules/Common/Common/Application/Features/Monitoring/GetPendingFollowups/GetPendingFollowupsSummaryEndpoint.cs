using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public class GetPendingFollowupsSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-followups/summary",
                async (
                    string? search,
                    string[]? slaStatus,
                    string[]? slaBucket,
                    string? pic,
                    string[]? purpose,
                    string[]? propertyType,
                    string[]? taskType,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingFollowupFilter(slaStatus, search, null, null, slaBucket, pic, purpose, propertyType, taskType);
                    var result = await sender.Send(new GetPendingFollowupsSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingFollowupsSummary")
            .Produces<MonitoringSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Followups summary KPIs")
            .WithDescription("Returns Total/Breached/AtRisk/Healthy counts for the Pending Followups tab. Requires any MONITORING:PENDING_FOLLOWUP* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingFollowup);
    }
}
