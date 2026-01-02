using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Services;

/// <summary>
/// Workflow service - handles orchestration, validation, persistence, and events
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Starts a new workflow instance
    /// </summary>
    Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? assignmentOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a workflow by completing the current activity
    /// </summary>
    Task<WorkflowInstance> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running workflow
    /// </summary>
    Task CancelWorkflowAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance with execution history
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instances assigned to a specific user
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current activities across all workflow instances for a user
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all current activities across workflow instances
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a workflow definition
    /// </summary>
    Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default);
}