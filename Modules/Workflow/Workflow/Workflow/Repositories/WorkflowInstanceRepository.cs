using Workflow.Data;
using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Workflow.Workflow.Repositories;

public class WorkflowInstanceRepository(WorkflowDbContext dbContext) : IWorkflowInstanceRepository
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

    public async Task<bool> TryUpdateWithConcurrencyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.WorkflowInstances.Update(instance);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Refresh the entity to get the latest values and reset change tracking
            await dbContext.Entry(instance).ReloadAsync(cancellationToken);
            return false;
        }
    }

    public async Task<WorkflowInstance?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Load the entity without change tracking for read-only scenarios
        // When we need to update, we'll load it again with tracking
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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

    public async Task<List<WorkflowInstance>> GetLongRunningWorkflowsAsync(
        TimeSpan threshold,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(threshold);
        
        return await dbContext.WorkflowInstances
            .Where(w => w.Status == WorkflowStatus.Running && w.CreatedOn <= cutoffTime)
            .OrderBy(w => w.CreatedOn)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowInstance>> GetActiveWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(w => w.Status == WorkflowStatus.Running || w.Status == WorkflowStatus.Suspended)
            .Include(w => w.WorkflowDefinition)
            .OrderByDescending(w => w.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowInstance>> GetWorkflowsOlderThanAsync(DateTime cutoffTime, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(w => w.Status == WorkflowStatus.Running && w.StartedOn <= cutoffTime)
            .Include(w => w.WorkflowDefinition)
            .OrderBy(w => w.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowInstance>> GetWorkflowsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(w => w.StartedOn >= since)
            .Include(w => w.WorkflowDefinition)
            .OrderByDescending(w => w.StartedOn)
            .ToListAsync(cancellationToken);
    }
}