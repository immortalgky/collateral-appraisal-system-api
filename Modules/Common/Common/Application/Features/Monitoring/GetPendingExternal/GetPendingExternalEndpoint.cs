using Carter;
using Common.Application.Features.Monitoring.GetPendingInternal;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

public class GetPendingExternalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-external",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string[]? slaStatus,
                    string? search,
                    string[]? activityId,
                    string? sortBy,
                    string? sortDir,
                    string[]? slaBucket,
                    string? pic,
                    string[]? purpose,
                    string[]? propertyType,
                    string[]? taskType,
                    string[]? appraisalCompanyId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingExternalFilter(slaStatus, search, activityId, sortBy, sortDir, slaBucket, pic, purpose, propertyType, taskType, appraisalCompanyId);
                    var result = await sender.Send(new GetPendingExternalQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingExternal")
            .Produces<PaginatedResult<PendingTaskDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending External tasks")
            .WithDescription("Returns external pending tasks scoped by the caller's layer permissions. Requires any MONITORING:PENDING_EXTERNAL:* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingExternal);
    }
}
