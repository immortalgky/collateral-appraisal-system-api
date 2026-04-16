namespace Workflow.Tasks.Features.OpenTask;

public class OpenTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/tasks/{taskId:guid}/open",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new OpenTaskCommand(taskId), cancellationToken);

                    if (result.IsSuccess)
                        return Results.Ok(result);

                    return result.ErrorMessage switch
                    {
                        "Task not found" => Results.NotFound(result),
                        "You are not assigned to this task" => Results.StatusCode(StatusCodes.Status403Forbidden),
                        _ => Results.BadRequest(result)
                    };
                }
            )
            .WithName("OpenTask")
            .Produces<OpenTaskResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Open and auto-start a task")
            .WithDescription(
                "Validates task ownership and transitions the task to In Progress. " +
                "For pool tasks, auto-claims and starts in one atomic operation. " +
                "Returns redirect data (AppraisalId, WorkflowInstanceId) for navigation. " +
                "Safe to call multiple times — pass-through if already In Progress.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
