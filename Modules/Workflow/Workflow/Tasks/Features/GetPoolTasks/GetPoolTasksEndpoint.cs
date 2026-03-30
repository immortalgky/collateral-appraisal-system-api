using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetPoolTasks;

public class GetPoolTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/pool",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [FromQuery] string? status,
                    [FromQuery] string? priority,
                    [FromQuery] string? taskName,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = new GetPoolTasksFilterRequest(
                        status,
                        priority,
                        taskName
                    );

                    var query = new GetPoolTasksQuery(pagination, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetPoolTasksResponse(result.Result));
                }
            )
            .WithName("GetPoolTasks")
            .Produces<GetPoolTasksResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get pool tasks for current user")
            .WithDescription(
                "Retrieves pool tasks where the current user is a member of the assigned group. Tasks appear here until claimed by a specific user.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
