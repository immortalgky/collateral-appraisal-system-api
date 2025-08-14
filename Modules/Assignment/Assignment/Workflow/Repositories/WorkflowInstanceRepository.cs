using Assignment.Data;
using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Assignment.Workflow.Repositories;

public class WorkflowInstanceRepository(AssignmentDbContext dbContext) : IWorkflowInstanceRepository
{
    public async Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByStatus(WorkflowStatus status,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.Status == status)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByAssignee(string assignee,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.CurrentAssignee == assignee && x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .OrderBy(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByWorkflowDefinition(Guid workflowDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.WorkflowDefinitionId == workflowDefinitionId)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowInstance?> GetByCorrelationId(string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }

    public async Task<WorkflowInstance?> GetWithExecutionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await dbContext.WorkflowInstances.AddAsync(instance, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetRunningInstances(CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        dbContext.WorkflowInstances.Update(instance);

        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await LoggerAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LoggerAsync(CancellationToken cancellationToken = default)
    {
        var changedEntries = dbContext.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Unchanged)
            .ToList();

        if (changedEntries.Any())
        {
            Console.WriteLine($"Saving {changedEntries.Count} changed entities");

            foreach (var entry in changedEntries)
            {
                var entityName = entry.Entity.GetType().Name;
                var keyValue = entry.Metadata.FindPrimaryKey()?.Properties
                    .Select(p => entry.Property(p.Name).CurrentValue)
                    .FirstOrDefault();

                Console.WriteLine($"Entity {entityName} (Key: {keyValue}) - State: {entry.State}");
            }
        }

        await Task.CompletedTask;
    }
}