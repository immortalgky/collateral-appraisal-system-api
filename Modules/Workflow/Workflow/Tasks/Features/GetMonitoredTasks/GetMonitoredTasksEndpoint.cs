using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredTasks;

public class GetMonitoredTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/monitor",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string[]? groupId,
                    [FromQuery] string[]? assigneeUsername,
                    [FromQuery] string[]? sla,
                    [FromQuery] string[]? activityId,
                    [FromQuery] string? search,
                    [FromQuery] string? appraisalNumber,
                    [FromQuery] string? customerName,
                    [FromQuery] string[]? appraisalStatus,
                    [FromQuery] string[]? taskType,
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetMonitoredTasksFilter(
                        groupId,
                        assigneeUsername,
                        sla,
                        activityId,
                        search,
                        appraisalNumber,
                        customerName,
                        appraisalStatus,
                        taskType,
                        sortBy,
                        sortDir);

                    var result = await sender.Send(new GetMonitoredTasksQuery(pagination, filter), cancellationToken);
                    return Results.Ok(new GetMonitoredTasksResponse(result.Result));
                }
            )
            .WithName("GetMonitoredTasks")
            .Produces<GetMonitoredTasksResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get tasks in monitored groups")
            .WithDescription(
                "Supervisor-facing task list. Returns person-assigned tasks (AssignedType=1, status Assigned/InProgress) " +
                "belonging to groups monitored by the authenticated user. Requires permission: task-monitor.view.")
            .WithTags("Tasks")
            .RequireAuthorization("task-monitor.view");
    }
}
