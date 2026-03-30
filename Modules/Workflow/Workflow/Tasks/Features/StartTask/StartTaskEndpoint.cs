namespace Workflow.Tasks.Features.StartTask;

public class StartTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/tasks/{taskId:guid}/start",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new StartTaskCommand(taskId), cancellationToken);

                    return result.IsSuccess
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                }
            )
            .WithName("StartTask")
            .Produces<StartTaskResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Start working on a task")
            .WithDescription(
                "Marks a task as in-progress by the current user. For pool tasks, pushes a real-time notification so other pool members see that someone is working on it.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
