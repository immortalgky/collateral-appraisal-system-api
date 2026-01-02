using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowExecutionLogRepository
{
    Task<List<WorkflowExecutionLog>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetByActivityAsync(
        Guid workflowInstanceId, 
        string activityId, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetByEventTypeAsync(
        ExecutionLogEvent eventType, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetByActorAsync(
        string actorId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default);
    
    Task<WorkflowExecutionLog?> GetLatestEventAsync(
        Guid workflowInstanceId, 
        ExecutionLogEvent eventType, 
        string? activityId = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetWorkflowTimelineAsync(
        Guid workflowInstanceId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task AddAsync(WorkflowExecutionLog logEntry, CancellationToken cancellationToken = default);
    
    Task AddRangeAsync(IEnumerable<WorkflowExecutionLog> logEntries, CancellationToken cancellationToken = default);
    
    Task<Dictionary<ExecutionLogEvent, int>> GetEventCountsAsync(
        Guid workflowInstanceId, 
        CancellationToken cancellationToken = default);
    
    Task<TimeSpan?> GetAverageActivityDurationAsync(
        string activityId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowExecutionLog>> GetFailedActivitiesAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<int> CleanupOldLogsAsync(
        TimeSpan retention, 
        CancellationToken cancellationToken = default);
}