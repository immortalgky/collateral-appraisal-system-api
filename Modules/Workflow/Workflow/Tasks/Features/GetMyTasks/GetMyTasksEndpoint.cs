using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMyTasks;

public class GetMyTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/me",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string? status,
                    [FromQuery] string? priority,
                    [FromQuery] string? taskName,
                    [FromQuery] string? appraisalNumber,
                    [FromQuery] string? customerName,
                    [FromQuery] string? taskStatus,
                    [FromQuery] string? taskType,
                    [FromQuery] DateTime? dateFrom,
                    [FromQuery] DateTime? dateTo,
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetMyTasksFilterRequest(
                        status,
                        priority,
                        taskName,
                        appraisalNumber,
                        customerName,
                        taskStatus,
                        taskType,
                        dateFrom,
                        dateTo,
                        sortBy,
                        sortDir
                    );

                    var query = new GetMyTasksQuery(pagination, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetMyTasksResponse(result.Result));
                }
            )
            .WithName("GetMyTasks")
            .Produces<GetMyTasksResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get current user's tasks")
            .WithDescription(
                "Retrieves workflow tasks assigned to the authenticated user with pagination and optional filtering by status, priority, and task name.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
