using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

public class GetPendingInternalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-internal",
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
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingInternalFilter(slaStatus, search, activityId, sortBy, sortDir, slaBucket, pic, purpose, propertyType, taskType);
                    var result = await sender.Send(new GetPendingInternalQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingInternal")
            .Produces<PaginatedResult<PendingTaskDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Internal tasks")
            .WithDescription("Returns internal pending tasks scoped by the caller's layer permissions. Requires any MONITORING:PENDING_INTERNAL:* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingInternal);
    }
}
