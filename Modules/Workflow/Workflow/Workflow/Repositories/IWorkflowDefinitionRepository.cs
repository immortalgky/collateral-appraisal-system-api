using Workflow.Workflow.Models;
using Shared.Data;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowDefinitionRepository : IRepository<WorkflowDefinition, Guid>
{
    Task<IEnumerable<WorkflowDefinition>> GetByCategory(string category, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitions(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<WorkflowDefinition?> GetByNameAndVersion(string name, int version,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition?> GetLatestVersion(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithName(string name, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}