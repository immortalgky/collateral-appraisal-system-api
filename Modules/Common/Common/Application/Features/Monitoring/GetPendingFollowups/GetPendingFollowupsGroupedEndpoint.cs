using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public class GetPendingFollowupsGroupedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-followups/grouped",
                async (
                    string groupBy,
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
                    if (string.IsNullOrWhiteSpace(groupBy) || groupBy.ToLowerInvariant() is not ("pic" or "company" or "activity"))
                        return Results.BadRequest("groupBy is required and must be one of: pic, company, activity.");

                    var filter = new PendingFollowupFilter(slaStatus, search, null, null, slaBucket, pic, purpose, propertyType, taskType);
                    var result = await sender.Send(new GetPendingFollowupsGroupedQuery(groupBy, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingFollowupsGrouped")
            .Produces<MonitoringGroupedResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Followups grouped view")
            .WithDescription("Returns followup tasks aggregated by ?groupBy=pic|company|activity. Breached/AtRisk computed from OLA columns. Requires any MONITORING:PENDING_FOLLOWUP* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingFollowup);
        }
}
