using Assignment.Workflow.Models;
using Shared.Data;

namespace Assignment.Workflow.Repositories;

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
}