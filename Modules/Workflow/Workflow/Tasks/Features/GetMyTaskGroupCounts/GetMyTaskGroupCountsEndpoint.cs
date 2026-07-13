using Microsoft.AspNetCore.Mvc;
using Workflow.Tasks.Features.GetMyTasks;

namespace Workflow.Tasks.Features.GetMyTaskGroupCounts;

public class GetMyTaskGroupCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/me/group-counts",
                async (
                    [FromQuery] string groupBy,
                    [FromQuery] string? activityId,
                    [FromQuery] string? status,
                    [FromQuery] string? priority,
                    [FromQuery] string? taskName,
                    [FromQuery] string? search,
                    [FromQuery] string? appraisalNumber,
                    [FromQuery] string? customerName,
                    [FromQuery] string? taskStatus,
                    [FromQuery] string? taskType,
                    [FromQuery] DateTime? dateFrom,
                    [FromQuery] DateTime? dateTo,
                    [FromQuery] DateTime? appointmentDateFrom,
                    [FromQuery] DateTime? appointmentDateTo,
                    [FromQuery] DateTime? requestedAtFrom,
                    [FromQuery] DateTime? requestedAtTo,
                    [FromQuery] string? slaStatus,
                    [FromQuery] string? purpose,
                    [FromQuery] string? taskStatusBucket,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetMyTasksFilterRequest(
                        ActivityId: activityId,
                        Status: status,
                        Priority: priority,
                        TaskName: taskName,
                        Search: search,
                        AppraisalNumber: appraisalNumber,
                        CustomerName: customerName,
                        TaskStatus: taskStatus,
                        TaskType: taskType,
                        DateFrom: dateFrom,
                        DateTo: dateTo,
                        AppointmentDateFrom: appointmentDateFrom,
                        AppointmentDateTo: appointmentDateTo,
                        RequestedAtFrom: requestedAtFrom,
                        RequestedAtTo: requestedAtTo,
                        SlaStatus: slaStatus,
                        Purpose: purpose,
                        TaskStatusBucket: taskStatusBucket
                    );

                    var query = new GetMyTaskGroupCountsQuery(groupBy, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetMyTaskGroupCountsResponse(result.Result));
                }
            )
            .WithName("GetMyTaskGroupCounts")
            .Produces<GetMyTaskGroupCountsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get task counts grouped by a column for the current user")
            .WithDescription(
                "Retrieves distinct value counts (only groups with count > 0) for one grouping dimension " +
                "(status, priority, purpose, activity, or slaStatus) over the current user's tasks, applying " +
                "the same filters as /tasks/me. Used to build Kanban columns from real data.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
