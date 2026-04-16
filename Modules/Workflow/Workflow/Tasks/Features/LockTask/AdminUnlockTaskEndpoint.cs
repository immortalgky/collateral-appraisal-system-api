namespace Workflow.Tasks.Features.LockTask;

public class AdminUnlockTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/tasks/{taskId:guid}/lock/admin",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new AdminUnlockTaskCommand(taskId), cancellationToken);

                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.BadRequest(new { message = result.ErrorMessage });
                }
            )
            .WithName("AdminUnlockTask")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Admin: force-release the lock on any pool task")
            .WithDescription("Releases any lock on a pool task regardless of who holds it. No ownership check is performed.")
            .WithTags("Tasks")
            .RequireAuthorization("CanReleaseTaskLocks");
    }
}
