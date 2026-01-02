using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public class WorkflowOutboxRepository : IWorkflowOutboxRepository
{
    private readonly WorkflowDbContext _context;

    public WorkflowOutboxRepository(WorkflowDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowOutbox?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowOutboxes
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetPendingEventsAsync(
        int maxCount = 100, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.Pending && 
                       (o.NextAttemptAt == null || o.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(o => o.OccurredAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetReadyForRetryAsync(
        int maxRetries = 5, 
        int maxCount = 100, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.Failed && 
                       o.Attempts < maxRetries &&
                       (o.NextAttemptAt == null || o.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(o => o.NextAttemptAt ?? o.OccurredAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowOutboxes
            .Where(o => o.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(o => o.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetByEventTypeAsync(
        string eventType, 
        OutboxStatus? status = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowOutboxes
            .Where(o => o.Type == eventType);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(o => o.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OccurredAt <= toDate.Value);

        return await query
            .OrderBy(o => o.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowOutboxes
            .Where(o => o.CorrelationId == correlationId)
            .OrderBy(o => o.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetFailedEventsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.Failed);

        if (fromDate.HasValue)
            query = query.Where(o => o.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OccurredAt <= toDate.Value);

        return await query
            .OrderByDescending(o => o.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowOutbox>> GetDeadLetterEventsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.DeadLetter);

        if (fromDate.HasValue)
            query = query.Where(o => o.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OccurredAt <= toDate.Value);

        return await query
            .OrderByDescending(o => o.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowOutbox outboxEvent, CancellationToken cancellationToken = default)
    {
        _context.WorkflowOutboxes.Add(outboxEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<WorkflowOutbox> outboxEvents, CancellationToken cancellationToken = default)
    {
        _context.WorkflowOutboxes.AddRange(outboxEvents);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryMarkAsProcessingAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var outboxEvent = await _context.WorkflowOutboxes
                .FirstOrDefaultAsync(o => o.Id == id && o.Status == OutboxStatus.Pending, cancellationToken);

            if (outboxEvent == null)
                return false;

            outboxEvent.MarkAsProcessing();
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another process is processing this event
            return false;
        }
    }

    public async Task MarkAsProcessedAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        var outboxEvent = await _context.WorkflowOutboxes
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.MarkAsProcessed();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(
        Guid id, 
        string errorMessage, 
        TimeSpan? retryDelay = null, 
        CancellationToken cancellationToken = default)
    {
        var outboxEvent = await _context.WorkflowOutboxes
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.MarkAsFailed(errorMessage, retryDelay);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsDeadLetterAsync(
        Guid id, 
        string reason, 
        CancellationToken cancellationToken = default)
    {
        var outboxEvent = await _context.WorkflowOutboxes
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (outboxEvent != null)
        {
            outboxEvent.MarkAsDeadLetter(reason);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateAsync(WorkflowOutbox outboxEvent, CancellationToken cancellationToken = default)
    {
        _context.WorkflowOutboxes.Update(outboxEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<OutboxStatus, int>> GetStatusCountsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowOutboxes.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(o => o.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OccurredAt <= toDate.Value);

        return await query
            .GroupBy(o => o.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    public async Task<int> CleanupProcessedEventsAsync(
        TimeSpan retention, 
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.Subtract(retention);
        
        var processedEvents = await _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.Processed && 
                       o.ProcessedAt.HasValue && 
                       o.ProcessedAt.Value <= cutoffDate)
            .ToListAsync(cancellationToken);

        _context.WorkflowOutboxes.RemoveRange(processedEvents);
        await _context.SaveChangesAsync(cancellationToken);
        
        return processedEvents.Count;
    }

    public async Task<int> MoveToDeadLetterAsync(
        int maxRetries, 
        CancellationToken cancellationToken = default)
    {
        var failedEvents = await _context.WorkflowOutboxes
            .Where(o => o.Status == OutboxStatus.Failed && o.Attempts >= maxRetries)
            .ToListAsync(cancellationToken);

        foreach (var failedEvent in failedEvents)
        {
            failedEvent.MarkAsDeadLetter($"Maximum retry attempts ({maxRetries}) exceeded");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return failedEvents.Count;
    }
}