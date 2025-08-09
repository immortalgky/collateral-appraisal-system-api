using Assignment.Data;
using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Assignment.Workflow.Repositories;

public class WorkflowDefinitionRepository : BaseRepository<WorkflowDefinition, Guid>, IWorkflowDefinitionRepository
{
    public WorkflowDefinitionRepository(AssignmentDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetByCategory(string category, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.Category == category)
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitions(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowDefinition?> GetByNameAndVersion(string name, int version, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(x => x.Name == name && x.Version == version, cancellationToken);
    }

    public async Task<WorkflowDefinition?> GetLatestVersion(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.Name == name)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithName(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(x => x.Name == name, cancellationToken);
    }
}