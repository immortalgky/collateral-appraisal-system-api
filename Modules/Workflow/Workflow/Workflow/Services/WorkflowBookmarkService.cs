using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

public class WorkflowBookmarkService : IWorkflowBookmarkService
{
    private readonly WorkflowDbContext _context;
    private readonly IWorkflowBookmarkRepository _bookmarkRepository;
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly IWorkflowExecutionLogRepository _executionLogRepository;
    private readonly ILogger<WorkflowBookmarkService> _logger;

    public WorkflowBookmarkService(
        WorkflowDbContext context,
        IWorkflowBookmarkRepository bookmarkRepository,
        IWorkflowInstanceRepository workflowRepository,
        IWorkflowExecutionLogRepository executionLogRepository,
        ILogger<WorkflowBookmarkService> logger)
    {
        _context = context;
        _bookmarkRepository = bookmarkRepository;
        _workflowRepository = workflowRepository;
        _executionLogRepository = executionLogRepository;
        _logger = logger;
    }

    public async Task<WorkflowBookmark> CreateUserActionBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user action bookmark for workflow {WorkflowInstanceId}, activity {ActivityId}, correlation {CorrelationId}",
            workflowInstanceId, activityId, correlationId);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Check for existing bookmark to prevent duplicates
            var existingBookmark = await _bookmarkRepository.FindUnconsumedBookmarkAsync(
                workflowInstanceId, activityId, key, BookmarkType.UserAction, cancellationToken);

            if (existingBookmark != null)
            {
                _logger.LogInformation("User action bookmark already exists: {BookmarkId}", existingBookmark.Id);
                return existingBookmark;
            }

            var bookmark = WorkflowBookmark.Create(
                workflowInstanceId,
                activityId,
                BookmarkType.UserAction,
                key,
                correlationId,
                payload);

            await _bookmarkRepository.AddAsync(bookmark, cancellationToken);

            // Log bookmark creation
            var logEntry = WorkflowExecutionLog.Create(
                workflowInstanceId,
                ExecutionLogEvent.BookmarkCreated,
                activityId,
                $"User action bookmark created: {key}",
                metadata: new Dictionary<string, object>
                {
                    ["bookmarkType"] = BookmarkType.UserAction.ToString(),
                    ["bookmarkKey"] = key,
                    ["bookmarkId"] = bookmark.Id
                });

            await _executionLogRepository.AddAsync(logEntry, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Created user action bookmark {BookmarkId} for workflow {WorkflowInstanceId}",
                bookmark.Id, workflowInstanceId);

            return bookmark;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating user action bookmark for workflow {WorkflowInstanceId}",
                workflowInstanceId);
            throw;
        }
    }

    public async Task<WorkflowBookmark> CreateTimerBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        DateTime dueAt,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating timer bookmark for workflow {WorkflowInstanceId}, activity {ActivityId}, due at {DueAt}, correlation {CorrelationId}",
            workflowInstanceId, activityId, dueAt, correlationId);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Check for existing timer bookmark
            var existingBookmark = await _bookmarkRepository.FindUnconsumedBookmarkAsync(
                workflowInstanceId, activityId, key, BookmarkType.Timer, cancellationToken);

            if (existingBookmark != null)
            {
                _logger.LogInformation("Timer bookmark already exists: {BookmarkId}", existingBookmark.Id);
                return existingBookmark;
            }

            var bookmark = WorkflowBookmark.Create(
                workflowInstanceId,
                activityId,
                BookmarkType.Timer,
                key,
                correlationId,
                payload,
                dueAt);

            await _bookmarkRepository.AddAsync(bookmark, cancellationToken);

            // Log timer bookmark creation
            var logEntry = WorkflowExecutionLog.Create(
                workflowInstanceId,
                ExecutionLogEvent.BookmarkCreated,
                activityId,
                $"Timer bookmark created: {key}, due at {dueAt}",
                metadata: new Dictionary<string, object>
                {
                    ["bookmarkType"] = BookmarkType.Timer.ToString(),
                    ["bookmarkKey"] = key,
                    ["bookmarkId"] = bookmark.Id,
                    ["dueAt"] = dueAt
                });

            await _executionLogRepository.AddAsync(logEntry, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Created timer bookmark {BookmarkId} for workflow {WorkflowInstanceId}",
                bookmark.Id, workflowInstanceId);

            return bookmark;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating timer bookmark for workflow {WorkflowInstanceId}",
                workflowInstanceId);
            throw;
        }
    }

    public async Task<WorkflowBookmark> CreateExternalMessageBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string? correlationId = null,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating external message bookmark for workflow {WorkflowInstanceId}, activity {ActivityId}, correlation {CorrelationId}",
            workflowInstanceId, activityId, correlationId);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingBookmark = await _bookmarkRepository.FindUnconsumedBookmarkAsync(
                workflowInstanceId, activityId, key, BookmarkType.ExternalMessage, cancellationToken);

            if (existingBookmark != null)
            {
                _logger.LogInformation("External message bookmark already exists: {BookmarkId}", existingBookmark.Id);
                return existingBookmark;
            }

            var bookmark = WorkflowBookmark.Create(
                workflowInstanceId,
                activityId,
                BookmarkType.ExternalMessage,
                key,
                correlationId,
                payload);

            await _bookmarkRepository.AddAsync(bookmark, cancellationToken);

            // Log bookmark creation
            var logEntry = WorkflowExecutionLog.Create(
                workflowInstanceId,
                ExecutionLogEvent.BookmarkCreated,
                activityId,
                $"External message bookmark created: {key}",
                metadata: new Dictionary<string, object>
                {
                    ["bookmarkType"] = BookmarkType.ExternalMessage.ToString(),
                    ["bookmarkKey"] = key,
                    ["bookmarkId"] = bookmark.Id
                });

            await _executionLogRepository.AddAsync(logEntry, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Created external message bookmark {BookmarkId} for workflow {WorkflowInstanceId}",
                bookmark.Id, workflowInstanceId);

            return bookmark;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating external message bookmark for workflow {WorkflowInstanceId}",
                workflowInstanceId);
            throw;
        }
    }

    public async Task<BookmarkConsumeResult> ConsumeBookmarkAsync(
        Guid workflowInstanceId,
        string activityId,
        string key,
        string consumedBy,
        Dictionary<string, object>? outputData = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Consuming bookmark for workflow {WorkflowInstanceId}, activity {ActivityId}, key {Key}",
            workflowInstanceId, activityId, key);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Validate workflow exists and is in runnable state
            var workflow = await _workflowRepository.GetForUpdateAsync(workflowInstanceId, cancellationToken);
            if (workflow == null)
            {
                return new BookmarkConsumeResult(false, null, "Workflow not found", 
                    BookmarkConsumeFailureReason.WorkflowNotFound);
            }

            if (workflow.Status != WorkflowStatus.Running)
            {
                return new BookmarkConsumeResult(false, null, 
                    $"Workflow is not in runnable state: {workflow.Status}", 
                    BookmarkConsumeFailureReason.WorkflowNotInRunnableState);
            }

            // Find the bookmark for any type (we'll determine the type)
            WorkflowBookmark? bookmark = null;
            foreach (var bookmarkType in Enum.GetValues<BookmarkType>())
            {
                bookmark = await _bookmarkRepository.FindUnconsumedBookmarkAsync(
                    workflowInstanceId, activityId, key, bookmarkType, cancellationToken);
                if (bookmark != null) break;
            }

            if (bookmark == null)
            {
                return new BookmarkConsumeResult(false, null, "Bookmark not found or already consumed", 
                    BookmarkConsumeFailureReason.BookmarkNotFound);
            }

            // Consume the bookmark
            bookmark.Consume(consumedBy);

            // Log bookmark consumption
            var logEntry = WorkflowExecutionLog.Create(
                workflowInstanceId,
                ExecutionLogEvent.BookmarkConsumed,
                activityId,
                $"Bookmark consumed: {key}",
                actorId: consumedBy,
                metadata: new Dictionary<string, object>
                {
                    ["bookmarkType"] = bookmark.Type.ToString(),
                    ["bookmarkKey"] = key,
                    ["bookmarkId"] = bookmark.Id,
                    ["outputData"] = outputData ?? new Dictionary<string, object>()
                });

            await _executionLogRepository.AddAsync(logEntry, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully consumed bookmark {BookmarkId} for workflow {WorkflowInstanceId}",
                bookmark.Id, workflowInstanceId);

            return new BookmarkConsumeResult(true, bookmark);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error consuming bookmark for workflow {WorkflowInstanceId}",
                workflowInstanceId);

            return new BookmarkConsumeResult(false, null, ex.Message);
        }
    }

    public async Task<List<WorkflowBookmark>> GetActiveBookmarksAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _bookmarkRepository.GetByWorkflowInstanceAsync(
            workflowInstanceId, onlyUnconsumed: true, cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetDueTimerBookmarksAsync(
        DateTime? upToTime = null,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var dueBookmarks = await _bookmarkRepository.GetDueTimersAsync(upToTime, cancellationToken);
        return dueBookmarks.Take(maxCount).ToList();
    }

    public async Task<List<WorkflowBookmark>> GetActivityBookmarksAsync(
        Guid workflowInstanceId,
        string activityId,
        bool onlyUnconsumed = true,
        CancellationToken cancellationToken = default)
    {
        return await _bookmarkRepository.GetByActivityAsync(
            workflowInstanceId, activityId, onlyUnconsumed, cancellationToken);
    }

    public async Task<int> CleanupExpiredBookmarksAsync(
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up bookmarks older than {Expiration}", expiration);
        
        var cleanedCount = await _bookmarkRepository.CleanupExpiredBookmarksAsync(expiration, cancellationToken);
        
        _logger.LogInformation("Cleaned up {CleanedCount} expired bookmarks", cleanedCount);
        return cleanedCount;
    }

    public async Task<bool> HasActiveBookmarksAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var activeBookmarks = await GetActiveBookmarksAsync(workflowInstanceId, cancellationToken);
        return activeBookmarks.Any();
    }
}