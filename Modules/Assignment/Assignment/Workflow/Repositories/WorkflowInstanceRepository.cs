using Assignment.Data;
using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Assignment.Workflow.Repositories;

public class WorkflowInstanceRepository : BaseRepository<WorkflowInstance, Guid>, IWorkflowInstanceRepository
{
    public WorkflowInstanceRepository(AssignmentDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByStatus(WorkflowStatus status, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.Status == status)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByAssignee(string assignee, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.CurrentAssignee == assignee && x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .OrderBy(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByWorkflowDefinition(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.WorkflowDefinitionId == workflowDefinitionId)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowInstance?> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }

    public async Task<WorkflowInstance?> GetWithExecutionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetRunningInstances(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .ToListAsync(cancellationToken);
    }
}