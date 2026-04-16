namespace Workflow.Tasks.Features.LockTask;

public class HeartbeatTaskLockEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/tasks/{taskId:guid}/lock/heartbeat",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new HeartbeatTaskLockCommand(taskId), cancellationToken);

                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.BadRequest(new { message = result.ErrorMessage });
                }
            )
            .WithName("HeartbeatTaskLock")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Refresh the editing lock on a pool task")
            .WithDescription("Resets the lock expiry timer. Must be called periodically by the lock owner to prevent automatic expiry after 30 minutes of inactivity.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
