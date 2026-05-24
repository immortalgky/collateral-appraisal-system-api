using Carter;
using Common.Application.Features.Monitoring.GetPendingInternal;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public class GetPendingFollowupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-followups",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string? search,
                    string[]? slaStatus,
                    string[]? slaBucket,
                    string? pic,
                    string[]? purpose,
                    string[]? propertyType,
                    string[]? taskType,
                    string? sortBy,
                    string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingFollowupFilter(slaStatus, search, sortBy, sortDir, slaBucket, pic, purpose, propertyType, taskType);
                    var result = await sender.Send(new GetPendingFollowupsQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingFollowups")
            .Produces<PaginatedResult<PendingTaskDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Follow Up list")
            .WithDescription("Returns open followup-type pending tasks (appraisal-initiation, appraisal-initiation-check, provide-additional-documents). Requires any MONITORING:PENDING_FOLLOWUP* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingFollowup);
    }
}
