namespace Workflow.Tasks.Features.ReassignTask;

public class ReassignTaskEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/tasks/{taskId:guid}/reassign",
                async (Guid taskId, ReassignTaskRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new ReassignTaskCommand(taskId, request.NewAssignedTo),
                        cancellationToken);

                    return result.IsSuccess
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                }
            )
            .WithName("ReassignTask")
            .Produces<ReassignTaskResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Reassign a task to another user")
            .WithDescription(
                "Supervisor-facing endpoint. Reassigns a person-assigned PendingTask from its current assignee " +
                "to another eligible user. DueAt and SLA state are preserved. Requires permission: task-monitor.reassign.")
            .WithTags("Tasks")
            .RequireAuthorization("task-monitor.reassign");
    }
}

public record ReassignTaskRequest(string NewAssignedTo);
