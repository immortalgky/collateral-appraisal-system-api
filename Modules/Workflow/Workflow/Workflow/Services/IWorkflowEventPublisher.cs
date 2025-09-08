using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

/// <summary>
/// Handles workflow event publishing - Service Layer responsibility
/// Manages event publishing for workflow lifecycle and activity events
/// </summary>
public interface IWorkflowEventPublisher
{
    /// <summary>
    /// Publishes workflow started event
    /// </summary>
    Task PublishWorkflowStartedAsync(
        Guid workflowInstanceId,
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        DateTime startedAt,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow completed event
    /// </summary>
    Task PublishWorkflowCompletedAsync(
        Guid workflowInstanceId,
        string completedBy,
        DateTime completedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow failed event
    /// </summary>
    Task PublishWorkflowFailedAsync(
        Guid workflowInstanceId,
        string? reason,
        DateTime failedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow cancelled event
    /// </summary>
    Task PublishWorkflowCancelledAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        DateTime cancelledAt,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow suspended event
    /// </summary>
    Task PublishWorkflowSuspendedAsync(
        Guid workflowInstanceId,
        string suspendedBy,
        DateTime suspendedAt,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow resumed event
    /// </summary>
    Task PublishWorkflowResumedAsync(
        Guid workflowInstanceId,
        string resumedBy,
        DateTime resumedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes activity started event
    /// </summary>
    Task PublishActivityStartedAsync(
        Guid workflowInstanceId,
        string activityId,
        string activityName,
        string? assignedTo,
        DateTime startedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes activity completed event
    /// </summary>
    Task PublishActivityCompletedAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        DateTime completedAt,
        Dictionary<string, object>? outputData = null,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes activity failed event
    /// </summary>
    Task PublishActivityFailedAsync(
        Guid workflowInstanceId,
        string activityId,
        string? errorMessage,
        DateTime failedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes activity assignment changed event
    /// </summary>
    Task PublishActivityAssignmentChangedAsync(
        Guid workflowInstanceId,
        string activityId,
        string? previousAssignee,
        string? newAssignee,
        DateTime changedAt,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes workflow state transition event
    /// </summary>
    Task PublishWorkflowStateTransitionAsync(
        Guid workflowInstanceId,
        WorkflowStatus previousState,
        WorkflowStatus newState,
        DateTime transitionAt,
        string? reason = null,
        CancellationToken cancellationToken = default);
}