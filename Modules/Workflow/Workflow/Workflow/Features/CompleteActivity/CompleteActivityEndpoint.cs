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
                CancellationToken cancellationToken) =>
            {
                var command = new CompleteActivityCommand
                {
                    WorkflowInstanceId = workflowInstanceId,
                    ActivityId = activityId,
                    CompletedBy = request.CompletedBy, // In real app, get from current user context
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

public record CompleteActivityCommand : ICommand<CompleteActivityResponse>
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
}

public class CompleteActivityCommandHandler : ICommandHandler<CompleteActivityCommand, CompleteActivityResponse>
{
    private readonly IWorkflowService _workflowService;

    public CompleteActivityCommandHandler(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public async Task<CompleteActivityResponse> Handle(CompleteActivityCommand request,
        CancellationToken cancellationToken)
    {
        // Get the workflow instance before resuming to capture the current assignee
        var currentWorkflowInstance =
            await _workflowService.GetWorkflowInstanceAsync(request.WorkflowInstanceId, cancellationToken);
        var currentAssignee = currentWorkflowInstance?.CurrentAssignee;

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

        var workflowInstance = await _workflowService.ResumeWorkflowAsync(
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