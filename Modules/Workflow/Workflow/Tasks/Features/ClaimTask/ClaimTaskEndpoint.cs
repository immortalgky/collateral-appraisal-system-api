namespace Workflow.Tasks.Features.ClaimTask;

public class ClaimTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/tasks/{taskId:guid}/claim",
                async (Guid taskId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new ClaimTaskCommand(taskId), cancellationToken);

                    return result.IsSuccess
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                }
            )
            .WithName("ClaimTask")
            .Produces<ClaimTaskResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Claim a pool task")
            .WithDescription(
                "Claims a pool task for the current user. Changes AssignedTo from the group to the user, and AssignedType from '2' (pool) to '1' (person). Pushes a real-time notification to other pool members.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
