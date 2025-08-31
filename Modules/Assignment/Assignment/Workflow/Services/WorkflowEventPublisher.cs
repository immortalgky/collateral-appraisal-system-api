using Assignment.Workflow.Events;
using Assignment.Workflow.Models;
using MassTransit;

namespace Assignment.Workflow.Services;

/// <summary>
/// Handles workflow event publishing - Service Layer responsibility
/// Manages event publishing for workflow lifecycle and activity events using MassTransit
/// </summary>
public class WorkflowEventPublisher : IWorkflowEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<WorkflowEventPublisher> _logger;

    public WorkflowEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<WorkflowEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishWorkflowStartedAsync(
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        DateTime startedAt,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowStarted
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowDefinitionId = workflowDefinitionId,
                InstanceName = instanceName,
                StartedBy = startedBy,
                StartedAt = startedAt,
                CorrelationId = correlationId
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowStarted event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowStarted event for {WorkflowInstanceId}", workflowInstanceId);
            // Don't rethrow - event publishing failures shouldn't break workflow execution
        }
    }

    public async Task PublishWorkflowCompletedAsync(
        Guid workflowInstanceId,
        string completedBy,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowCompleted
            {
                WorkflowInstanceId = workflowInstanceId,
                CompletedBy = completedBy,
                CompletedAt = completedAt
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowCompleted event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowCompleted event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    public async Task PublishWorkflowFailedAsync(
        Guid workflowInstanceId,
        string? reason,
        DateTime failedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowFailed
            {
                WorkflowInstanceId = workflowInstanceId,
                Reason = reason ?? "Unknown error",
                FailedAt = failedAt
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowFailed event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowFailed event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    public async Task PublishWorkflowCancelledAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        DateTime cancelledAt,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowCancelled
            {
                WorkflowInstanceId = workflowInstanceId,
                CancelledBy = cancelledBy,
                CancelledAt = cancelledAt,
                Reason = reason
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowCancelled event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowCancelled event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    public async Task PublishWorkflowSuspendedAsync(
        Guid workflowInstanceId,
        string suspendedBy,
        DateTime suspendedAt,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowSuspended
            {
                WorkflowInstanceId = workflowInstanceId,
                SuspendedBy = suspendedBy,
                SuspendedAt = suspendedAt,
                Reason = reason
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowSuspended event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowSuspended event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    public async Task PublishWorkflowResumedAsync(
        Guid workflowInstanceId,
        string resumedBy,
        DateTime resumedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowResumed
            {
                WorkflowInstanceId = workflowInstanceId,
                ResumedBy = resumedBy,
                ResumedAt = resumedAt
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowResumed event for {WorkflowInstanceId}", workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowResumed event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    public async Task PublishActivityStartedAsync(
        Guid workflowInstanceId,
        string activityId,
        string activityName,
        string? assignedTo,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowActivityStarted
            {
                WorkflowInstanceId = workflowInstanceId,
                ActivityId = activityId,
                ActivityName = activityName,
                AssignedTo = assignedTo,
                StartedAt = startedAt
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowActivityStarted event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowActivityStarted event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
    }

    public async Task PublishActivityCompletedAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        DateTime completedAt,
        Dictionary<string, object>? outputData = null,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowActivityCompleted
            {
                WorkflowInstanceId = workflowInstanceId,
                ActivityId = activityId,
                CompletedBy = completedBy,
                CompletedAt = completedAt,
                OutputData = outputData ?? new Dictionary<string, object>(),
                Comments = comments
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowActivityCompleted event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowActivityCompleted event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
    }

    public async Task PublishActivityFailedAsync(
        Guid workflowInstanceId,
        string activityId,
        string? errorMessage,
        DateTime failedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowActivityFailed
            {
                WorkflowInstanceId = workflowInstanceId,
                ActivityId = activityId,
                ErrorMessage = errorMessage,
                FailedAt = failedAt
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowActivityFailed event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowActivityFailed event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
    }

    public async Task PublishActivityAssignmentChangedAsync(
        Guid workflowInstanceId,
        string activityId,
        string? previousAssignee,
        string? newAssignee,
        DateTime changedAt,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowActivityAssignmentChanged
            {
                WorkflowInstanceId = workflowInstanceId,
                ActivityId = activityId,
                PreviousAssignee = previousAssignee,
                NewAssignee = newAssignee,
                ChangedAt = changedAt,
                Reason = reason
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowActivityAssignmentChanged event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowActivityAssignmentChanged event for {ActivityId} in {WorkflowInstanceId}", 
                activityId, workflowInstanceId);
        }
    }

    public async Task PublishWorkflowStateTransitionAsync(
        Guid workflowInstanceId,
        WorkflowStatus previousState,
        WorkflowStatus newState,
        DateTime transitionAt,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new WorkflowStateTransition
            {
                WorkflowInstanceId = workflowInstanceId,
                PreviousState = previousState.ToString(),
                NewState = newState.ToString(),
                TransitionAt = transitionAt,
                Reason = reason
            }, cancellationToken);

            _logger.LogDebug("Published WorkflowStateTransition event for {WorkflowInstanceId}: {PreviousState} -> {NewState}", 
                workflowInstanceId, previousState, newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish WorkflowStateTransition event for {WorkflowInstanceId}", workflowInstanceId);
        }
    }
}

// Event DTOs for new events not already defined in WorkflowService
public record WorkflowCompleted
{
    public Guid WorkflowInstanceId { get; init; }
    public string CompletedBy { get; init; } = default!;
    public DateTime CompletedAt { get; init; }
}

public record WorkflowFailed
{
    public Guid WorkflowInstanceId { get; init; }
    public string Reason { get; init; } = default!;
    public DateTime FailedAt { get; init; }
}

public record WorkflowSuspended
{
    public Guid WorkflowInstanceId { get; init; }
    public string SuspendedBy { get; init; } = default!;
    public DateTime SuspendedAt { get; init; }
    public string Reason { get; init; } = default!;
}

public record WorkflowResumed
{
    public Guid WorkflowInstanceId { get; init; }
    public string ResumedBy { get; init; } = default!;
    public DateTime ResumedAt { get; init; }
}

public record WorkflowActivityStarted
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string ActivityName { get; init; } = default!;
    public string? AssignedTo { get; init; }
    public DateTime StartedAt { get; init; }
}

public record WorkflowActivityFailed
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string? ErrorMessage { get; init; }
    public DateTime FailedAt { get; init; }
}

public record WorkflowActivityAssignmentChanged
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string? PreviousAssignee { get; init; }
    public string? NewAssignee { get; init; }
    public DateTime ChangedAt { get; init; }
    public string? Reason { get; init; }
}

public record WorkflowStateTransition
{
    public Guid WorkflowInstanceId { get; init; }
    public string PreviousState { get; init; } = default!;
    public string NewState { get; init; } = default!;
    public DateTime TransitionAt { get; init; }
    public string? Reason { get; init; }
}