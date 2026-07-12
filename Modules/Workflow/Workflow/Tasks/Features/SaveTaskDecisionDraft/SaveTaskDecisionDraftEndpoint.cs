namespace Workflow.Tasks.Features.SaveTaskDecisionDraft;

public class SaveTaskDecisionDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/tasks/{taskId:guid}/decision-draft",
                async (
                    Guid taskId,
                    SaveTaskDecisionDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new SaveTaskDecisionDraftCommand(
                            taskId,
                            request.DecisionTaken,
                            request.Comment,
                            request.ReasonCode,
                            request.Assignee),
                        cancellationToken);

                    if (result.IsForbidden)
                        return Results.Forbid();

                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.BadRequest(new { message = result.ErrorMessage });
                }
            )
            .WithName("SaveTaskDecisionDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Save an in-progress decision draft for a task")
            .WithDescription(
                "Persists the task owner's in-progress decision (action, comment, reason code, and next assignee) " +
                "on the PendingTask so it survives page reloads. Does not affect the final decision recorded on " +
                "completion (CompletedTask). Only the task owner may save; returns 403 otherwise.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}

public record SaveTaskDecisionDraftRequest(
    string? DecisionTaken,
    string? Comment,
    string? ReasonCode,
    string? Assignee);
