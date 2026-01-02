using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowOutboxRepository
{
    Task<WorkflowOutbox?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetPendingEventsAsync(
        int maxCount = 100, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetReadyForRetryAsync(
        int maxRetries = 5, 
        int maxCount = 100, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetByEventTypeAsync(
        string eventType, 
        OutboxStatus? status = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetFailedEventsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowOutbox>> GetDeadLetterEventsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task AddAsync(WorkflowOutbox outboxEvent, CancellationToken cancellationToken = default);
    
    Task AddRangeAsync(IEnumerable<WorkflowOutbox> outboxEvents, CancellationToken cancellationToken = default);
    
    Task<bool> TryMarkAsProcessingAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
    
    Task MarkAsProcessedAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
    
    Task MarkAsFailedAsync(
        Guid id, 
        string errorMessage, 
        TimeSpan? retryDelay = null, 
        CancellationToken cancellationToken = default);
    
    Task MarkAsDeadLetterAsync(
        Guid id, 
        string reason, 
        CancellationToken cancellationToken = default);
    
    Task UpdateAsync(WorkflowOutbox outboxEvent, CancellationToken cancellationToken = default);
    
    Task<Dictionary<OutboxStatus, int>> GetStatusCountsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<int> CleanupProcessedEventsAsync(
        TimeSpan retention, 
        CancellationToken cancellationToken = default);
    
    Task<int> MoveToDeadLetterAsync(
        int maxRetries, 
        CancellationToken cancellationToken = default);
}