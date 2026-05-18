using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredPeople;

public class GetMonitoredPeopleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/monitor/people",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string? search,
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetMonitoredPeopleFilter(search, sortBy, sortDir);
                    var result = await sender.Send(
                        new GetMonitoredPeopleQuery(pagination, filter), cancellationToken);
                    return Results.Ok(new GetMonitoredPeopleResponse(result.Result));
                }
            )
            .WithName("GetMonitoredPeople")
            .Produces<GetMonitoredPeopleResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get people the supervisor monitors, with task counts")
            .WithDescription(
                "Supervisor-facing people list. Returns one row per user the authenticated supervisor monitors " +
                "(via auth.GroupMonitoring), with OpenTasks (InProgress), AvailableTasks (Assigned), and " +
                "TotalTasks counts derived from workflow.vw_TaskMonitor. Requires permission: task-monitor.view.")
            .WithTags("Tasks")
            .RequireAuthorization("task-monitor.view");
    }
}
