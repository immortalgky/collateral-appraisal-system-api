using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowActivityExecutionRepository
{
    /// <summary>
    /// Gets current activities (InProgress status) for a specific user
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all current activities (InProgress status) across workflows
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current activity for a specific workflow instance
    /// </summary>
    Task<WorkflowActivityExecution?> GetCurrentActivityForWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all activity executions for a workflow instance
    /// </summary>
    Task<IEnumerable<WorkflowActivityExecution>> GetExecutionsForWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new activity execution record
    /// </summary>
    Task AddAsync(WorkflowActivityExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing activity execution record
    /// </summary>
    Task UpdateAsync(WorkflowActivityExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}