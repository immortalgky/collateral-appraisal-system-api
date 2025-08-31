using Assignment.Data;
using Assignment.Workflow.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Workflow.Repositories;

public class WorkflowActivityExecutionRepository : IWorkflowActivityExecutionRepository
{
    private readonly AssignmentDbContext _context;

    public WorkflowActivityExecutionRepository(AssignmentDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowActivityExecutions
            .Where(ae => ae.AssignedTo == userId && ae.Status == ActivityExecutionStatus.InProgress)
            .Include(ae => ae.WorkflowInstance)
            .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderBy(ae => ae.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowActivityExecutions
            .Where(ae => ae.Status == ActivityExecutionStatus.InProgress)
            .Include(ae => ae.WorkflowInstance)
            .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderBy(ae => ae.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowActivityExecution?> GetCurrentActivityForWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowActivityExecutions
            .Where(ae => ae.WorkflowInstanceId == workflowInstanceId && ae.Status == ActivityExecutionStatus.InProgress)
            .Include(ae => ae.WorkflowInstance)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetExecutionsForWorkflowAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowActivityExecutions
            .Where(ae => ae.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(ae => ae.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowActivityExecution execution, CancellationToken cancellationToken = default)
    {
        await _context.WorkflowActivityExecutions.AddAsync(execution, cancellationToken);
    }

    public async Task UpdateAsync(WorkflowActivityExecution execution, CancellationToken cancellationToken = default)
    {
        _context.WorkflowActivityExecutions.Update(execution);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}