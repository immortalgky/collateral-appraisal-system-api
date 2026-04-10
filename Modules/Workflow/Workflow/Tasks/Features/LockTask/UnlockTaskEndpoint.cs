namespace Workflow.Tasks.Features.LockTask;

public class UnlockTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/tasks/{taskId:guid}/lock",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new UnlockTaskCommand(taskId), cancellationToken);

                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.BadRequest(new { message = result.ErrorMessage });
                }
            )
            .WithName("UnlockTask")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Release the editing lock on a pool task")
            .WithDescription("Releases the lock held by the current user on a pool task. Only the lock owner may call this endpoint.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
