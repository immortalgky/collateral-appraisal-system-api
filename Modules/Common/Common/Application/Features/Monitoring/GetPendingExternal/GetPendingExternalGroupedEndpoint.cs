using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

public class GetPendingExternalGroupedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-external/grouped",
                async (
                    string groupBy,
                    string[]? slaStatus,
                    string? search,
                    string[]? activityId,
                    string[]? slaBucket,
                    string? pic,
                    string[]? purpose,
                    string[]? propertyType,
                    string[]? taskType,
                    string[]? appraisalCompanyId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(groupBy) || groupBy.ToLowerInvariant() is not ("pic" or "company" or "activity"))
                        return Results.BadRequest("groupBy is required and must be one of: pic, company, activity.");

                    var filter = new PendingExternalFilter(slaStatus, search, activityId, null, null, slaBucket, pic, purpose, propertyType, taskType, appraisalCompanyId);
                    var result = await sender.Send(new GetPendingExternalGroupedQuery(groupBy, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingExternalGrouped")
            .Produces<MonitoringGroupedResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending External grouped view")
            .WithDescription("Returns rows aggregated by ?groupBy=pic|company|activity with Breached/AtRisk counts. Returns up to 200 groups ordered by Count DESC. Requires any MONITORING:PENDING_EXTERNAL:* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingExternal);
    }
}
