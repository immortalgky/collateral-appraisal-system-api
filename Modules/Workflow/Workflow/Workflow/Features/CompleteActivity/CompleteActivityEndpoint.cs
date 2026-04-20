using Shared.CQRS;
using Shared.Identity;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Services;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Features.CompleteActivity;

public class CompleteActivityEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/complete", async (
                Guid workflowInstanceId,
                string activityId,
                CompleteActivityRequest request,
                ISender sender,
                ICurrentUserService currentUserService,
                CancellationToken cancellationToken) =>
            {
                var command = new CompleteActivityCommand
                {
                    WorkflowInstanceId = workflowInstanceId,
                    ActivityId = activityId,
                    CompletedBy = currentUserService.Username!,
                    Input = request.Input,
                    NextAssignmentOverrides = request.NextAssignmentOverrides
                };

                try
                {
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                }
                catch (WorkflowActionFailedException ex)
                {
                    // B4: Action failure — the transaction has already been rolled back by
                    // TransactionalBehavior. Map to a 422 Problem Details response so callers
                    // receive structured failure info rather than a 500.
                    return Results.Problem(
                        title: "Activity completion failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: new Dictionary<string, object?>
                        {
                            ["stepName"] = ex.Failure.StepName,
                            ["errorCode"] = ex.Failure.ErrorCode,
                        });
                }
            })
            .WithName("CompleteActivity")
            .WithTags("Workflows");
    }
}

public record CompleteActivityRequest
{
    public string CompletedBy { get; init; } = default!;
    public Dictionary<string, object> Input { get; init; } = new();

    /// <summary>
    /// Assignment overrides for upcoming activities
    /// Key = ActivityId, Value = Assignment override details
    /// </summary>
    public Dictionary<string, AssignmentOverrideRequest>? NextAssignmentOverrides { get; init; }
}

/// <summary>
/// Request model for assignment overrides
/// </summary>
public record AssignmentOverrideRequest
{
    /// <summary>
    /// Specific user to assign the task to
    /// </summary>
    public string? RuntimeAssignee { get; init; }

    /// <summary>
    /// Specific group to assign the task to
    /// </summary>
    public string? RuntimeAssigneeGroup { get; init; }

    /// <summary>
    /// Custom assignment strategies to use
    /// </summary>
    public List<string>? RuntimeAssignmentStrategies { get; init; }

    /// <summary>
    /// Reason for the override
    /// </summary>
    public string? OverrideReason { get; init; }

    /// <summary>
    /// Additional properties to override
    /// </summary>
    public Dictionary<string, object>? OverrideProperties { get; init; }
}

public record CompleteActivityCommand : ICommand<CompleteActivityResponse>, ITransactionalCommand<IWorkflowUnitOfWork>
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;
    public Dictionary<string, object> Input { get; init; } = new();
    public Dictionary<string, AssignmentOverrideRequest>? NextAssignmentOverrides { get; init; }
}

public record CompleteActivityResponse
{
    public Guid WorkflowInstanceId { get; init; }
    public Guid WorkflowActivityExecutionId { get; init; }
    public string Status { get; init; } = default!;
    public string? NextActivityId { get; init; }
    public string? CurrentAssignee { get; init; }
    public string? NextAssignee { get; init; }
    public bool IsCompleted { get; init; }
    public List<string>? ValidationErrors { get; init; }
}

public class CompleteActivityCommandHandler(
    IWorkflowService workflowService,
    IActivityProcessPipeline processPipeline,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CompleteActivityCommand, CompleteActivityResponse>
{
    public async Task<CompleteActivityResponse> Handle(CompleteActivityCommand request,
        CancellationToken cancellationToken)
    {
        // Get the workflow instance before resuming to capture the current assignee
        var currentWorkflowInstance =
            await workflowService.GetWorkflowInstanceAsync(request.WorkflowInstanceId, cancellationToken);
        var currentAssignee = currentWorkflowInstance?.CurrentAssignee;

        // Build a typed read-only input dict for the pipeline
        var input = request.Input
            .ToDictionary<KeyValuePair<string, object>, string, object?>(
                kv => kv.Key, kv => kv.Value);

        // B1: Generate a stable execution ID so trace rows can be looked up via the admin API.
        var workflowActivityExecutionId = Guid.CreateVersion7();

        // W10: Pull roles from the authenticated user rather than hardcoding an empty array.
        var userRoles = currentUserService.Roles;

        // Run the configurable process pipeline INSIDE the completion transaction.
        // The pipeline includes Validation steps (collect-all) and Action steps (stop-on-first).
        // RequireDocumentFollowupClearedStep is now a registered Validation step — no inline gate needed.
        var pipelineResult = await processPipeline.ExecuteAsync(
            request.WorkflowInstanceId,
            workflowActivityExecutionId,
            request.ActivityId,
            request.CompletedBy,
            userRoles,
            input,
            cancellationToken);

        // B4: Distinguish Validation failures (no mutation) from Action failures (need rollback).
        if (!pipelineResult.IsSuccess)
        {
            if (pipelineResult.ActionFailure is not null)
            {
                // Action failure: mutations may have occurred — force a rollback via exception.
                throw new WorkflowActionFailedException(pipelineResult.ActionFailure);
            }

            // Validation failure: nothing was mutated, return a clean failure response.
            return new CompleteActivityResponse
            {
                WorkflowInstanceId = request.WorkflowInstanceId,
                WorkflowActivityExecutionId = workflowActivityExecutionId,
                Status = "ValidationFailed",
                ValidationErrors = pipelineResult.AllErrors().ToList()
            };
        }

        // Convert assignment override requests to runtime override objects
        Dictionary<string, RuntimeOverride>? nextRuntimeOverrides = null;
        if (request.NextAssignmentOverrides != null)
        {
            nextRuntimeOverrides = new Dictionary<string, RuntimeOverride>();
            foreach (var kvp in request.NextAssignmentOverrides)
            {
                var activityId = kvp.Key;
                var overrideRequest = kvp.Value;

                nextRuntimeOverrides[activityId] = new RuntimeOverride
                {
                    RuntimeAssignee = overrideRequest.RuntimeAssignee,
                    RuntimeAssigneeGroup = overrideRequest.RuntimeAssigneeGroup,
                    RuntimeAssignmentStrategies = overrideRequest.RuntimeAssignmentStrategies,
                    OverrideReason = overrideRequest.OverrideReason,
                    OverrideProperties = overrideRequest.OverrideProperties,
                    OverrideBy = request.CompletedBy,
                    CreatedAt = dateTimeProvider.ApplicationNow
                };
            }
        }

        var workflowInstance = await workflowService.ResumeWorkflowAsync(
            request.WorkflowInstanceId,
            request.ActivityId,
            request.CompletedBy,
            request.Input,
            nextRuntimeOverrides,
            cancellationToken);

        return new CompleteActivityResponse
        {
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowActivityExecutionId = workflowActivityExecutionId,
            Status = workflowInstance.Status.ToString(),
            NextActivityId = workflowInstance.CurrentActivityId,
            CurrentAssignee = currentAssignee,
            NextAssignee = workflowInstance.CurrentAssignee,
            IsCompleted = workflowInstance.Status == Models.WorkflowStatus.Completed
        };
    }
}
