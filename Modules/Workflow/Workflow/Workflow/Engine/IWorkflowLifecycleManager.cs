using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Manages workflow lifecycle operations - Core orchestration responsibility
/// Handles workflow state transitions, lifecycle events, and execution coordination
/// </summary>
public interface IWorkflowLifecycleManager
{
    /// <summary>
    /// Initializes a new workflow instance and prepares it for execution
    /// </summary>
    WorkflowInstance InitializeWorkflowAsync(
        Guid workflowDefinitionId,
        WorkflowSchema workflowSchema,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? runtimeOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions workflow to a new state and updates tracking
    /// </summary>
    Task<bool> TransitionWorkflowStateAsync(
        WorkflowInstance workflowInstance,
        WorkflowStatus newStatus,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances workflow to the next activity
    /// </summary>
    Task<bool> AdvanceWorkflowAsync(
        WorkflowInstance workflowInstance,
        string nextActivityId,
        string? assignee = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the workflow and performs cleanup
    /// </summary>
    Task<bool> CompleteWorkflowAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the workflow execution
    /// </summary>
    Task<bool> PauseWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused workflow
    /// </summary>
    Task<bool> ResumeWorkflowAsync(WorkflowInstance workflowInstance,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the workflow execution
    /// </summary>
    Task<bool> TerminateWorkflowAsync(
        WorkflowInstance workflowInstance,
        string reason,
        string terminatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a workflow state transition is allowed
    /// </summary>
    bool CanTransitionTo(WorkflowStatus currentStatus, WorkflowStatus targetStatus);
}