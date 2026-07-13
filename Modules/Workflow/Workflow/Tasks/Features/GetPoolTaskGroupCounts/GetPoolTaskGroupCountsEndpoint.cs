using Microsoft.AspNetCore.Mvc;
using Workflow.Tasks.Features.GetPoolTasks;

namespace Workflow.Tasks.Features.GetPoolTaskGroupCounts;

public class GetPoolTaskGroupCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/pool/group-counts",
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
                    var filter = new GetPoolTasksFilterRequest(
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
                        ActivityId: activityId,
                        Purpose: purpose,
                        TaskStatusBucket: taskStatusBucket
                    );

                    var query = new GetPoolTaskGroupCountsQuery(groupBy, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetPoolTaskGroupCountsResponse(result.Result));
                }
            )
            .WithName("GetPoolTaskGroupCounts")
            .Produces<GetPoolTaskGroupCountsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get pool task counts grouped by a column for the current user")
            .WithDescription(
                "Retrieves distinct value counts (only groups with count > 0) for one grouping dimension " +
                "(status, priority, purpose, activity, or slaStatus) over the current user's pool tasks, applying " +
                "the same filters as /tasks/pool. Used to build Kanban columns from real data.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
