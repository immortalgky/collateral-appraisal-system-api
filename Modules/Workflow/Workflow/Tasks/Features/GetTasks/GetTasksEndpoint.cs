using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string? status,
                    [FromQuery] string? assigneeUserId,
                    [FromQuery] string? priority,
                    [FromQuery] string? taskName,
                    [FromQuery] string? activityId,
                    [FromQuery] string? appraisalNumber,
                    [FromQuery] string? customerName,
                    [FromQuery] string? taskStatus,
                    [FromQuery] string? taskType,
                    [FromQuery] DateTime? dateFrom,
                    [FromQuery] DateTime? dateTo,
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    [FromQuery] string? slaStatus,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetTasksFilterRequest(
                        status,
                        assigneeUserId,
                        priority,
                        taskName,
                        activityId,
                        appraisalNumber,
                        customerName,
                        taskStatus,
                        taskType,
                        dateFrom,
                        dateTo,
                        sortBy,
                        sortDir,
                        slaStatus
                    );

                    var query = new GetTasksQuery(pagination, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetTasksResponse(result.Result));
                }
            )
            .WithName("GetTasks")
            .Produces<GetTasksResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all tasks")
            .WithDescription(
                "Retrieves all workflow tasks with pagination and optional filtering by status, assignee, priority, and task name.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}