using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

public interface IWorkflowBookmarkService
{
    /// <summary>
    /// Create a bookmark for human interaction (user task, approval, etc.)
    /// </summary>
    Task<WorkflowBookmark> CreateUserActionBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a timer bookmark for delayed execution or timeout handling
    /// </summary>
    Task<WorkflowBookmark> CreateTimerBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        DateTime dueAt,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a bookmark for waiting on external messages or events
    /// </summary>
    Task<WorkflowBookmark> CreateExternalMessageBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume workflow by consuming a bookmark
    /// </summary>
    Task<BookmarkConsumeResult> ConsumeBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string consumedBy,
        Dictionary<string, object>? outputData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active bookmarks for a workflow instance
    /// </summary>
    Task<List<WorkflowBookmark>> GetActiveBookmarksAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get due timer bookmarks that need to be processed
    /// </summary>
    Task<List<WorkflowBookmark>> GetDueTimerBookmarksAsync(
        DateTime? upToTime = null,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get bookmarks by activity for specific workflow
    /// </summary>
    Task<List<WorkflowBookmark>> GetActivityBookmarksAsync(
        Guid workflowInstanceId,
        string activityId,
        bool onlyUnconsumed = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up expired bookmarks
    /// </summary>
    Task<int> CleanupExpiredBookmarksAsync(
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if workflow has any active bookmarks (is waiting for something)
    /// </summary>
    Task<bool> HasActiveBookmarksAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}

public sealed record BookmarkConsumeResult(
    bool Success,
    WorkflowBookmark? Bookmark = null,
    string? ErrorMessage = null,
    BookmarkConsumeFailureReason? FailureReason = null
);

public enum BookmarkConsumeFailureReason
{
    BookmarkNotFound,
    BookmarkAlreadyConsumed,
    WorkflowNotFound,
    WorkflowNotInRunnableState,
    ActivityNotFound,
    ValidationFailed
}