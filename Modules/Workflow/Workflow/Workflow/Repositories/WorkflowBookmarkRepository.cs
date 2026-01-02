using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Workflow.Models;
using Shared.Data;
using Dapper;
using System.Data;

namespace Workflow.Workflow.Repositories;

public class WorkflowBookmarkRepository : IWorkflowBookmarkRepository
{
    private readonly WorkflowDbContext _context;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public WorkflowBookmarkRepository(WorkflowDbContext context, ISqlConnectionFactory sqlConnectionFactory)
    {
        _context = context;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<WorkflowBookmark?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowBookmarks
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<WorkflowBookmark?> GetByKeyAsync(
        string key, 
        BookmarkType type, 
        bool onlyUnconsumed = true, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowBookmarks
            .Where(b => b.Key == key && b.Type == type);

        if (onlyUnconsumed)
            query = query.Where(b => !b.IsConsumed);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetByWorkflowInstanceAsync(
        Guid workflowInstanceId, 
        bool onlyUnconsumed = false, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowBookmarks
            .Where(b => b.WorkflowInstanceId == workflowInstanceId);

        if (onlyUnconsumed)
            query = query.Where(b => !b.IsConsumed);

        return await query
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetByActivityAsync(
        Guid workflowInstanceId, 
        string activityId, 
        bool onlyUnconsumed = true, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowBookmarks
            .Where(b => b.WorkflowInstanceId == workflowInstanceId && b.ActivityId == activityId);

        if (onlyUnconsumed)
            query = query.Where(b => !b.IsConsumed);

        return await query
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetDueTimersAsync(
        DateTime? upToTime = null, 
        CancellationToken cancellationToken = default)
    {
        var checkTime = upToTime ?? DateTime.UtcNow;
        
        return await _context.WorkflowBookmarks
            .Where(b => b.Type == BookmarkType.Timer 
                     && !b.IsConsumed 
                     && b.DueAt.HasValue 
                     && b.DueAt.Value <= checkTime)
            .OrderBy(b => b.DueAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetExpiredBookmarksAsync(
        TimeSpan expiration, 
        CancellationToken cancellationToken = default)
    {
        var expirationTime = DateTime.UtcNow.Subtract(expiration);
        
        return await _context.WorkflowBookmarks
            .Where(b => !b.IsConsumed && b.CreatedAt <= expirationTime)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowBookmark?> FindUnconsumedBookmarkAsync(
        Guid workflowInstanceId, 
        string activityId, 
        string key, 
        BookmarkType type, 
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowBookmarks
            .FirstOrDefaultAsync(b => 
                b.WorkflowInstanceId == workflowInstanceId &&
                b.ActivityId == activityId &&
                b.Key == key &&
                b.Type == type &&
                !b.IsConsumed, cancellationToken);
    }

    public async Task AddAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _context.WorkflowBookmarks.Add(bookmark);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryConsumeBookmarkAsync(
        Guid bookmarkId, 
        string consumedBy, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bookmark = await _context.WorkflowBookmarks
                .FirstOrDefaultAsync(b => b.Id == bookmarkId && !b.IsConsumed, cancellationToken);

            if (bookmark == null)
                return false;

            bookmark.Consume(consumedBy);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another process consumed the bookmark
            return false;
        }
    }

    public async Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _context.WorkflowBookmarks.Update(bookmark);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _context.WorkflowBookmarks.Remove(bookmark);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<WorkflowBookmark>> GetDueTimerBookmarksAsync(
        int maxCount = 100,
        DateTime? upToTime = null,
        CancellationToken cancellationToken = default)
    {
        var currentTime = upToTime ?? DateTime.UtcNow;
        
        return await _context.WorkflowBookmarks
            .Where(b => !b.IsConsumed && 
                       b.DueAt.HasValue && 
                       b.DueAt.Value <= currentTime)
            .OrderBy(b => b.DueAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredBookmarksAsync(
        TimeSpan expiration, 
        CancellationToken cancellationToken = default)
    {
        var expirationTime = DateTime.UtcNow.Subtract(expiration);
        
        var expiredBookmarks = await _context.WorkflowBookmarks
            .Where(b => b.IsConsumed && b.ConsumedAt.HasValue && b.ConsumedAt.Value <= expirationTime)
            .ToListAsync(cancellationToken);

        _context.WorkflowBookmarks.RemoveRange(expiredBookmarks);
        await _context.SaveChangesAsync(cancellationToken);
        
        return expiredBookmarks.Count;
    }

    public async Task<WorkflowBookmark?> ClaimNextAvailableBookmarkAsync(
        BookmarkType type,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        using var connection = _sqlConnectionFactory.CreateNewConnection();
        
        var sql = @"
            UPDATE TOP(1) wb
            SET wb.ClaimedBy = @workerId, 
                wb.ClaimedAt = GETUTCDATE(), 
                wb.LeaseExpiresAt = DATEADD(second, @leaseSeconds, GETUTCDATE())
            OUTPUT INSERTED.*
            FROM WorkflowBookmarks wb
            WHERE wb.Type = @type 
              AND wb.IsConsumed = 0 
              AND (wb.ClaimedBy IS NULL OR wb.LeaseExpiresAt < GETUTCDATE())
            ORDER BY wb.CreatedAt";

        var parameters = new
        {
            workerId,
            type = type.ToString(),
            leaseSeconds = (int)leaseDuration.TotalSeconds
        };

        var bookmark = await connection.QueryFirstOrDefaultAsync<WorkflowBookmark>(
            sql, parameters, commandTimeout: 30);

        if (bookmark != null)
        {
            // Attach to context for tracking
            _context.Attach(bookmark);
        }

        return bookmark;
    }

    public async Task<bool> TryConsumeBookmarkWithLockAsync(
        Guid bookmarkId,
        string consumedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = _sqlConnectionFactory.CreateNewConnection();
        
        // Use pessimistic locking to ensure atomic consumption
        var sql = @"
            BEGIN TRANSACTION;
            
            DECLARE @result BIT = 0;
            
            -- Acquire lock and check if bookmark can be consumed
            SELECT @result = 1
            FROM WorkflowBookmarks WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @bookmarkId 
              AND IsConsumed = 0
              AND (ClaimedBy IS NULL OR ClaimedBy = @consumedBy OR LeaseExpiresAt < GETUTCDATE());
            
            -- If bookmark is available, consume it
            IF @result = 1
            BEGIN
                UPDATE WorkflowBookmarks
                SET IsConsumed = 1,
                    ConsumedAt = GETUTCDATE(),
                    ConsumedBy = @consumedBy
                WHERE Id = @bookmarkId;
            END
            
            COMMIT TRANSACTION;
            
            SELECT @result;";

        var parameters = new { bookmarkId, consumedBy };
        
        var result = await connection.ExecuteScalarAsync<bool>(
            sql, parameters, commandTimeout: 30);

        return result;
    }

    public async Task<List<WorkflowBookmark>> GetBookmarksByCorrelationAsync(
        string correlationId,
        bool onlyUnconsumed = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WorkflowBookmarks
            .Where(b => b.CorrelationId == correlationId);

        if (onlyUnconsumed)
            query = query.Where(b => !b.IsConsumed);

        return await query
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReleaseClaimAsync(
        Guid bookmarkId,
        string claimedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = _sqlConnectionFactory.CreateNewConnection();
        
        var sql = @"
            UPDATE WorkflowBookmarks
            SET ClaimedBy = NULL,
                ClaimedAt = NULL,
                LeaseExpiresAt = NULL
            WHERE Id = @bookmarkId 
              AND ClaimedBy = @claimedBy
              AND IsConsumed = 0";

        var parameters = new { bookmarkId, claimedBy };
        
        var affectedRows = await connection.ExecuteAsync(
            sql, parameters, commandTimeout: 30);

        return affectedRows > 0;
    }
}