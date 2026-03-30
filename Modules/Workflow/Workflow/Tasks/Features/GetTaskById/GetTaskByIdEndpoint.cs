namespace Workflow.Tasks.Features.GetTaskById;

public class GetTaskByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/{taskId:guid}",
                async (
                    Guid taskId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetTaskByIdQuery(taskId);
                    var result = await sender.Send(query, cancellationToken);

                    if (!result.IsOwner)
                        return Results.Forbid();

                    return Results.Ok(result);
                })
            .WithName("GetTaskById")
            .Produces<TaskDetailResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a task by ID")
            .WithDescription(
                "Retrieves a single pending workflow task by its ID. Returns 404 if not found, 403 if the task is not assigned to the current user.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
