using Shared.CQRS;
using Shared.Identity;
using Workflow.Data.Repository;
using Workflow.DocumentFollowups.Application;
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

                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result);
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
    IDocumentFollowupGate documentFollowupGate,
    IAssignmentRepository assignmentRepository) : ICommandHandler<CompleteActivityCommand, CompleteActivityResponse>
{
    public async Task<CompleteActivityResponse> Handle(CompleteActivityCommand request,
        CancellationToken cancellationToken)
    {
        // Run the configurable process pipeline before resuming the workflow
        var pipelineResult = await processPipeline.ExecuteAsync(
            request.WorkflowInstanceId,
            request.ActivityId,
            request.CompletedBy,
            request.Input,
            cancellationToken);

        if (!pipelineResult.Success)
        {
            return new CompleteActivityResponse
            {
                WorkflowInstanceId = request.WorkflowInstanceId,
                Status = "ValidationFailed",
                ValidationErrors = pipelineResult.Errors
            };
        }

        // Get the workflow instance before resuming to capture the current assignee
        var currentWorkflowInstance =
            await workflowService.GetWorkflowInstanceAsync(request.WorkflowInstanceId, cancellationToken);
        var currentAssignee = currentWorkflowInstance?.CurrentAssignee;

        // Document followup gate: if this activity opted in via canRaiseFollowup,
        // block submission while open followups exist for the corresponding pending task.
        if (currentWorkflowInstance is not null &&
            ActivityFollowupHelpers.ActivityCanRaiseFollowup(currentWorkflowInstance, request.ActivityId))
        {
            var correlationGuid = !string.IsNullOrEmpty(currentWorkflowInstance.CorrelationId) &&
                                  Guid.TryParse(currentWorkflowInstance.CorrelationId, out var parsed)
                ? parsed
                : currentWorkflowInstance.Id;

            var taskName = ActivityFollowupHelpers.ResolveActivityName(currentWorkflowInstance, request.ActivityId)
                           ?? request.ActivityId;

            var pendingTask = await assignmentRepository.GetPendingTaskAsync(
                correlationGuid, taskName, cancellationToken);

            if (pendingTask is not null &&
                await documentFollowupGate.HasOpenFollowupAsync(pendingTask.Id, cancellationToken))
            {
                return new CompleteActivityResponse
                {
                    WorkflowInstanceId = request.WorkflowInstanceId,
                    Status = "ValidationFailed",
                    ValidationErrors = new List<string>
                    {
                        "This task has open document followups. Resolve or cancel them before submitting."
                    }
                };
            }
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
                    CreatedAt = DateTime.UtcNow
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
            Status = workflowInstance.Status.ToString(),
            NextActivityId = workflowInstance.CurrentActivityId,
            CurrentAssignee = currentAssignee,
            NextAssignee = workflowInstance.CurrentAssignee,
            IsCompleted = workflowInstance.Status == Models.WorkflowStatus.Completed
        };
    }
}