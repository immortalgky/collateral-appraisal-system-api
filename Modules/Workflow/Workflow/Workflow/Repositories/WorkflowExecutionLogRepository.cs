using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public class WorkflowExecutionLogRepository : IWorkflowExecutionLogRepository
{
    private readonly WorkflowDbContext _context;

    public WorkflowExecutionLogRepository(WorkflowDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkflowExecutionLog>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutionLogs
            .Where(l => l.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecutionLog>> GetByActivityAsync(
        Guid workflowInstanceId, 
        string activityId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutionLogs
            .Where(l => l.WorkflowInstanceId == workflowInstanceId && l.ActivityId == activityId)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecutionLog>> GetByEventTypeAsync(
        ExecutionLogEvent eventType, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.Event == eventType);

        if (fromDate.HasValue)
            query = query.Where(l => l.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.OccurredAt <= toDate.Value);

        return await query
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecutionLog>> GetByActorAsync(
        string actorId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.ActorId == actorId);

        if (fromDate.HasValue)
            query = query.Where(l => l.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.OccurredAt <= toDate.Value);

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecutionLog>> GetByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutionLogs
            .Where(l => l.CorrelationId == correlationId)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowExecutionLog?> GetLatestEventAsync(
        Guid workflowInstanceId, 
        ExecutionLogEvent eventType, 
        string? activityId = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.WorkflowInstanceId == workflowInstanceId && l.Event == eventType);

        if (!string.IsNullOrEmpty(activityId))
            query = query.Where(l => l.ActivityId == activityId);

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WorkflowExecutionLog>> GetWorkflowTimelineAsync(
        Guid workflowInstanceId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.WorkflowInstanceId == workflowInstanceId);

        if (fromDate.HasValue)
            query = query.Where(l => l.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.OccurredAt <= toDate.Value);

        return await query
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowExecutionLog logEntry, CancellationToken cancellationToken = default)
    {
        _context.WorkflowExecutionLogs.Add(logEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<WorkflowExecutionLog> logEntries, CancellationToken cancellationToken = default)
    {
        _context.WorkflowExecutionLogs.AddRange(logEntries);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<ExecutionLogEvent, int>> GetEventCountsAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowExecutionLogs
            .Where(l => l.WorkflowInstanceId == workflowInstanceId)
            .GroupBy(l => l.Event)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    public async Task<TimeSpan?> GetAverageActivityDurationAsync(
        string activityId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.ActivityId == activityId 
                     && l.Event == ExecutionLogEvent.ActivityCompleted 
                     && l.Duration.HasValue);

        if (fromDate.HasValue)
            query = query.Where(l => l.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.OccurredAt <= toDate.Value);

        var durations = await query
            .Select(l => l.Duration!.Value)
            .ToListAsync(cancellationToken);

        if (!durations.Any())
            return null;

        var averageTicks = durations.Average(d => d.Ticks);
        return new TimeSpan((long)averageTicks);
    }

    public async Task<List<WorkflowExecutionLog>> GetFailedActivitiesAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowExecutionLogs
            .Where(l => l.Event == ExecutionLogEvent.ActivityFailed);

        if (fromDate.HasValue)
            query = query.Where(l => l.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.OccurredAt <= toDate.Value);

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CleanupOldLogsAsync(
        TimeSpan retention, 
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.Subtract(retention);
        
        var oldLogs = await _context.WorkflowExecutionLogs
            .Where(l => l.OccurredAt <= cutoffDate)
            .ToListAsync(cancellationToken);

        _context.WorkflowExecutionLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync(cancellationToken);
        
        return oldLogs.Count;
    }
}