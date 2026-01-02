using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public interface IWorkflowBookmarkRepository
{
    Task<WorkflowBookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<WorkflowBookmark?> GetByKeyAsync(
        string key, 
        BookmarkType type, 
        bool onlyUnconsumed = true, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        bool onlyUnconsumed = false, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetByActivityAsync(
        Guid workflowInstanceId, 
        string activityId, 
        bool onlyUnconsumed = true, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetDueTimersAsync(
        DateTime? upToTime = null, 
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetDueTimerBookmarksAsync(
        int maxCount = 100,
        DateTime? upToTime = null,
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetExpiredBookmarksAsync(
        TimeSpan expiration, 
        CancellationToken cancellationToken = default);
    
    Task<WorkflowBookmark?> FindUnconsumedBookmarkAsync(
        Guid workflowInstanceId, 
        string activityId, 
        string key, 
        BookmarkType type, 
        CancellationToken cancellationToken = default);
    
    Task AddAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default);
    
    Task<bool> TryConsumeBookmarkAsync(
        Guid bookmarkId, 
        string consumedBy, 
        CancellationToken cancellationToken = default);
    
    Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default);
    
    Task<int> CleanupExpiredBookmarksAsync(
        TimeSpan expiration, 
        CancellationToken cancellationToken = default);
    
    // New atomic operations for queue-like processing
    Task<WorkflowBookmark?> ClaimNextAvailableBookmarkAsync(
        BookmarkType type,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    
    Task<bool> TryConsumeBookmarkWithLockAsync(
        Guid bookmarkId,
        string consumedBy,
        CancellationToken cancellationToken = default);
    
    Task<List<WorkflowBookmark>> GetBookmarksByCorrelationAsync(
        string correlationId,
        bool onlyUnconsumed = true,
        CancellationToken cancellationToken = default);
    
    Task<bool> ReleaseClaimAsync(
        Guid bookmarkId,
        string claimedBy,
        CancellationToken cancellationToken = default);
}