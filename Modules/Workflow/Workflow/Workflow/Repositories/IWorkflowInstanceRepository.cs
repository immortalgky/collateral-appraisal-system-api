using Workflow.Workflow.Models;
using Shared.Data;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<WorkflowInstance>> GetByStatus(WorkflowStatus status,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<WorkflowInstance>> GetByAssignee(string assignee, CancellationToken cancellationToken = default);

    Task<IEnumerable<WorkflowInstance>> GetByWorkflowDefinition(Guid workflowDefinitionId,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstance?> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default);
    Task<WorkflowInstance?> GetWithExecutionsAsync(Guid id, CancellationToken cancellationToken = default);


    Task AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task LoggerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance with optimistic locking for update operations
    /// </summary>
    Task<WorkflowInstance?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to update a workflow instance with concurrency handling
    /// </summary>
    Task<bool> TryUpdateWithConcurrencyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflows that have been running longer than the specified duration
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetLongRunningWorkflowsAsync(TimeSpan timeout, int maxResults, CancellationToken cancellationToken = default);
}