using Shared.Identity;
using Workflow.Tasks.Features.AdvanceFanOutStage;

namespace Workflow.Tasks.Features.AdvanceFanOutStage;

public class AdvanceFanOutStageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Direct task-id form — used by callers that already hold the PendingTaskId
        // (e.g. task-list / task-detail FE pages).
        app.MapPost(
                "/tasks/{taskId:guid}/actions/{actionValue}",
                async (
                    Guid taskId,
                    string actionValue,
                    AdvanceFanOutStageRequest? request,
                    ISender sender,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrEmpty(currentUserService.Username))
                        return Results.Unauthorized();

                    var command = new AdvanceFanOutStageCommand(
                        PendingTaskId: taskId,
                        ActionValue: actionValue,
                        CompletedBy: currentUserService.Username,
                        AdditionalInput: request?.AdditionalInput);

                    var result = await sender.Send(command, cancellationToken);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(new { result.ErrorMessage });
                })
            .WithName("TakeFanOutStageAction")
            .WithTags("Tasks")
            .WithSummary("Take a stage action on a fan-out task by task id")
            .RequireAuthorization();

        // Workflow-context form — used by feature handlers that only know the triple
        // (e.g. ext-company quotation FE chains "save quotation" → this endpoint without
        // having to look up the PendingTask first).
        app.MapPost(
                "/workflows/{workflowInstanceId:guid}/activities/{activityId}/companies/{companyId:guid}/actions/{actionValue}",
                async (
                    Guid workflowInstanceId,
                    string activityId,
                    Guid companyId,
                    string actionValue,
                    AdvanceFanOutStageRequest? request,
                    ISender sender,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrEmpty(currentUserService.Username))
                        return Results.Unauthorized();

                    var command = new AdvanceFanOutStageCommand(
                        PendingTaskId: null,
                        ActionValue: actionValue,
                        CompletedBy: currentUserService.Username,
                        AdditionalInput: request?.AdditionalInput,
                        WorkflowInstanceId: workflowInstanceId,
                        ActivityId: activityId,
                        CompanyId: companyId);

                    var result = await sender.Send(command, cancellationToken);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(new { result.ErrorMessage });
                })
            .WithName("TakeFanOutStageActionByContext")
            .WithTags("Tasks")
            .WithSummary("Take a stage action on a fan-out task by workflow context")
            .WithDescription(
                "Resolves the PendingTask via (WorkflowInstanceId, ActivityId, CompanyId) and applies the action. " +
                "Useful for feature flows that don't hold the PendingTaskId directly.")
            .RequireAuthorization();
    }
}

public record AdvanceFanOutStageRequest(
    Dictionary<string, object>? AdditionalInput = null
);
