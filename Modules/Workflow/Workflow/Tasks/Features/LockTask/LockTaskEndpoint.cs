namespace Workflow.Tasks.Features.LockTask;

public class LockTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/tasks/{taskId:guid}/lock",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new LockTaskCommand(taskId), cancellationToken);

                    if (!result.IsSuccess)
                    {
                        if (result.ErrorMessage != null && result.ErrorMessage.StartsWith("Task is locked by"))
                            return Results.Conflict(new { message = result.ErrorMessage });

                        return Results.BadRequest(new { message = result.ErrorMessage });
                    }

                    return Results.Ok(new { lockedBy = result.LockedBy, lockedAt = result.LockedAt });
                }
            )
            .WithName("LockTask")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Lock a pool task for editing")
            .WithDescription("Acquires an editing lock on a pool task. First-write-wins: if another user already holds the lock a 409 Conflict is returned.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
