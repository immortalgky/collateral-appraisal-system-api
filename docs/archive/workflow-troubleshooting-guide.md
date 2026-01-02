# Workflow Troubleshooting Guide
## Complete Problem Resolution and Maintenance Guide

### Table of Contents
1. [Common Issues and Solutions](#common-issues-and-solutions)
2. [Performance Issues](#performance-issues)
3. [Database-Related Problems](#database-related-problems)
4. [Background Services Issues](#background-services-issues)
5. [API and Integration Problems](#api-and-integration-problems)
6. [Monitoring and Diagnostics](#monitoring-and-diagnostics)
7. [Maintenance Procedures](#maintenance-procedures)
8. [Emergency Response](#emergency-response)

---

## Common Issues and Solutions

### 1. Workflow Stuck in Pending State

**Symptoms**:
- Workflow instance shows "Suspended" or "Pending" status indefinitely
- Activities not progressing despite completion attempts
- Users report tasks not appearing in their queues

**Possible Causes**:
```sql
-- Check for unconsumed bookmarks
SELECT wb.*, wi.Name as WorkflowName 
FROM WorkflowBookmark wb
JOIN WorkflowInstance wi ON wb.WorkflowInstanceId = wi.Id
WHERE wb.IsConsumed = 0 
  AND wb.DueAt IS NULL OR wb.DueAt <= GETUTCDATE()
ORDER BY wb.CreatedAt DESC;

-- Check for activities without proper assignment
SELECT wae.*, wi.Name as WorkflowName
FROM WorkflowActivityExecution wae
JOIN WorkflowInstance wi ON wae.WorkflowInstanceId = wi.Id
WHERE wae.Status = 'Pending' 
  AND (wae.AssignedTo IS NULL OR wae.AssignedTo = '')
ORDER BY wae.StartedOn DESC;
```

**Solutions**:

1. **Check Bookmark Status**:
```csharp
// Via API or direct database access
var stuckBookmarks = await _bookmarkRepository.GetUnconsumedBookmarksAsync();
foreach (var bookmark in stuckBookmarks)
{
    if (bookmark.Type == BookmarkType.Timer && bookmark.DueAt <= DateTime.UtcNow)
    {
        // Manually trigger timer completion
        await _workflowService.ResumeWorkflowAsync(
            bookmark.WorkflowInstanceId,
            bookmark.ActivityId,
            "System.Recovery",
            new Dictionary<string, object> { ["TimerCompleted"] = true }
        );
    }
}
```

2. **Reassign Pending Activities**:
```csharp
// Fix assignment issues
var pendingActivities = await _activityRepository.GetPendingActivitiesWithoutAssignmentAsync();
foreach (var activity in pendingActivities)
{
    // Reassign using default assignment strategy
    var assignee = await _assignmentService.GetDefaultAssigneeAsync(activity.ActivityType);
    activity.AssignTo(assignee);
    await _activityRepository.UpdateAsync(activity);
}
```

3. **Manual Workflow Recovery**:
```sql
-- Emergency database fix for stuck workflows
UPDATE WorkflowInstance 
SET Status = 'Suspended', 
    CurrentActivityId = 'target-activity-id',
    CurrentAssignee = 'recovery-user@company.com'
WHERE Id = 'stuck-workflow-id' 
  AND Status = 'Running';

-- Create recovery bookmark if needed
INSERT INTO WorkflowBookmark (Id, WorkflowInstanceId, ActivityId, Type, Key, IsConsumed, CreatedAt)
VALUES (NEWID(), 'stuck-workflow-id', 'target-activity-id', 'UserAction', 'recovery-bookmark', 0, GETUTCDATE());
```

### 2. Concurrency Conflicts

**Symptoms**:
- API returns 409 Conflict responses
- "ConcurrencyToken mismatch" errors
- Activities completing but changes not persisting

**Diagnosis**:
```csharp
// Check for concurrent modifications
var conflictingUpdates = await _executionLogRepository.GetLogsByWorkflowAsync(workflowId)
    .Where(log => log.Event.Contains("ConcurrencyConflict"))
    .OrderByDescending(log => log.At)
    .Take(10);

foreach (var log in conflictingUpdates)
{
    Console.WriteLine($"Conflict at {log.At}: {log.Details}");
}
```

**Solutions**:

1. **Implement Retry Logic in Client**:
```csharp
public async Task<bool> CompleteActivityWithRetry(
    Guid workflowInstanceId, 
    string activityId, 
    Dictionary<string, object> input,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await _workflowService.ResumeWorkflowAsync(
                workflowInstanceId, activityId, "user@company.com", input);
            return true;
        }
        catch (OptimisticConcurrencyException ex)
        {
            if (attempt == maxRetries) throw;
            
            // Exponential backoff
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            
            // Refresh entity before retry
            var freshInstance = await _workflowService.GetWorkflowInstanceAsync(workflowInstanceId);
            // Use fresh concurrency token in retry
        }
    }
    return false;
}
```

2. **Database Cleanup for Orphaned Locks**:
```sql
-- Find activities that might have stale locks
SELECT wae.Id, wae.WorkflowInstanceId, wae.StartedOn, wae.Status,
       DATEDIFF(HOUR, wae.StartedOn, GETUTCDATE()) as HoursOld
FROM WorkflowActivityExecution wae
WHERE wae.Status = 'InProgress' 
  AND DATEDIFF(HOUR, wae.StartedOn, GETUTCDATE()) > 24;

-- Clean up stale in-progress activities (use with caution)
UPDATE WorkflowActivityExecution 
SET Status = 'Pending',
    StartedOn = GETUTCDATE()
WHERE Status = 'InProgress' 
  AND DATEDIFF(HOUR, StartedOn, GETUTCDATE()) > 24;
```

### 3. Assignment Issues

**Symptoms**:
- Activities assigned to wrong users
- Tasks not appearing in user task lists
- "No available assignee found" errors

**Diagnosis**:
```csharp
// Check assignment service health
var assignmentReport = await _assignmentService.GetAssignmentHealthReportAsync();
Console.WriteLine($"Available assignees: {assignmentReport.AvailableAssignees}");
Console.WriteLine($"Overloaded assignees: {assignmentReport.OverloadedAssignees}");
Console.WriteLine($"Assignment failures: {assignmentReport.RecentFailures.Count}");
```

**Solutions**:

1. **Fix Assignment Strategy Issues**:
```csharp
// Override failed assignments
var failedAssignments = await _activityRepository.GetActivitiesWithoutValidAssignmentAsync();
foreach (var activity in failedAssignments)
{
    try
    {
        // Try round-robin assignment as fallback
        var fallbackAssignee = await _roundRobinSelector.SelectAssigneeAsync(activity);
        if (fallbackAssignee != null)
        {
            activity.AssignTo(fallbackAssignee.UserId);
            await _activityRepository.UpdateAsync(activity);
            
            // Log the override
            await _executionLogRepository.CreateAsync(new WorkflowExecutionLog
            {
                WorkflowInstanceId = activity.WorkflowInstanceId,
                ActivityId = activity.ActivityId,
                Event = "AssignmentOverride",
                At = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["OriginalStrategy"] = "Failed",
                    ["FallbackStrategy"] = "RoundRobin",
                    ["NewAssignee"] = fallbackAssignee.UserId
                }
            });
        }
    }
    catch (Exception ex)
    {
        // Log and skip this assignment for manual review
        _logger.LogError(ex, "Failed to fix assignment for activity {ActivityId}", activity.Id);
    }
}
```

2. **User Group Synchronization**:
```csharp
// Refresh user group memberships
await _userGroupService.SynchronizeUserGroupsAsync();

// Verify group memberships for problem users
var problematicUsers = new[] { "user1@company.com", "user2@company.com" };
foreach (var userId in problematicUsers)
{
    var groups = await _userGroupService.GetUserGroupsAsync(userId);
    Console.WriteLine($"User {userId} groups: {string.Join(", ", groups)}");
    
    // Check if user should be in required groups
    var requiredGroups = new[] { "appraisers", "reviewers" };
    var missingGroups = requiredGroups.Except(groups).ToList();
    if (missingGroups.Any())
    {
        Console.WriteLine($"User {userId} missing groups: {string.Join(", ", missingGroups)}");
        // Add to missing groups if needed
    }
}
```

---

## Performance Issues

### 1. Slow Workflow Startup

**Symptoms**:
- Workflow startup taking > 30 seconds
- Timeout errors during workflow initialization
- High CPU usage during workflow creation

**Diagnosis**:
```sql
-- Analyze workflow startup performance
SELECT TOP 100
    wl.WorkflowInstanceId,
    wi.Name,
    MIN(wl.At) as StartTime,
    MAX(wl.At) as LastActivity,
    DATEDIFF(MILLISECOND, MIN(wl.At), MAX(wl.At)) as DurationMs,
    COUNT(*) as EventCount
FROM WorkflowExecutionLog wl
JOIN WorkflowInstance wi ON wl.WorkflowInstanceId = wi.Id
WHERE wl.At >= DATEADD(HOUR, -24, GETUTCDATE())
GROUP BY wl.WorkflowInstanceId, wi.Name
HAVING DATEDIFF(MILLISECOND, MIN(wl.At), MAX(wl.At)) > 30000
ORDER BY DurationMs DESC;
```

**Solutions**:

1. **Database Query Optimization**:
```sql
-- Add missing indexes
CREATE NONCLUSTERED INDEX IX_WorkflowInstance_WorkflowDefinitionId_Status 
ON WorkflowInstance(WorkflowDefinitionId, Status) 
INCLUDE (Id, Name, StartedOn, CurrentActivityId);

CREATE NONCLUSTERED INDEX IX_WorkflowExecutionLog_WorkflowInstanceId_At 
ON WorkflowExecutionLog(WorkflowInstanceId, At) 
INCLUDE (ActivityId, Event, Details);

CREATE NONCLUSTERED INDEX IX_WorkflowActivityExecution_WorkflowInstanceId_Status 
ON WorkflowActivityExecution(WorkflowInstanceId, Status) 
INCLUDE (ActivityId, StartedOn, AssignedTo);
```

2. **Caching Implementation**:
```csharp
// Cache workflow definitions
public class CachedWorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var cacheKey = $"workflow-definition-{id}";
        
        if (_cache.TryGetValue(cacheKey, out WorkflowDefinition? cached))
            return cached;

        var definition = await _repository.GetByIdAsync(id, cancellationToken);
        if (definition != null)
        {
            _cache.Set(cacheKey, definition, _cacheExpiry);
        }
        
        return definition;
    }
}
```

3. **Background Pre-loading**:
```csharp
// Pre-load commonly used workflow definitions
public class WorkflowDefinitionPreloader : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var popularDefinitions = await _repository.GetMostUsedDefinitionsAsync(10, cancellationToken);
        foreach (var definition in popularDefinitions)
        {
            // Pre-compile and cache workflow schemas
            await _workflowEngine.ValidateWorkflowDefinitionAsync(definition.Schema, cancellationToken);
            
            // Pre-warm assignment strategies
            await _assignmentService.PreloadStrategiesForDefinitionAsync(definition.Id);
        }
    }
}
```

### 2. High Memory Usage

**Symptoms**:
- Application memory usage > 2GB
- OutOfMemoryExceptions
- Frequent garbage collection

**Diagnosis**:
```csharp
// Memory diagnostics
public class MemoryDiagnostics
{
    public static void LogMemoryUsage(ILogger logger)
    {
        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64 / 1024 / 1024; // MB
        var privateMemory = process.PrivateMemorySize64 / 1024 / 1024; // MB
        
        logger.LogInformation("Memory Usage - Working Set: {WorkingSet}MB, Private: {PrivateMemory}MB", 
            workingSet, privateMemory);
            
        // Check garbage collection
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        
        logger.LogInformation("GC Collections - Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}", 
            gen0, gen1, gen2);
    }
}
```

**Solutions**:

1. **Entity Framework Optimization**:
```csharp
// Use read-only queries for reporting
public async Task<List<WorkflowSummaryDto>> GetWorkflowSummariesAsync()
{
    return await _context.WorkflowInstances
        .AsNoTracking() // Don't track changes
        .Where(wi => wi.Status != WorkflowStatus.Completed)
        .Select(wi => new WorkflowSummaryDto // Project only needed fields
        {
            Id = wi.Id,
            Name = wi.Name,
            Status = wi.Status.ToString(),
            StartedOn = wi.StartedOn
        })
        .ToListAsync();
}

// Dispose contexts properly
public async Task ProcessLargeDataSetAsync()
{
    const int batchSize = 100;
    var processed = 0;
    
    while (true)
    {
        using var context = new WorkflowDbContext(_options);
        var batch = await context.WorkflowInstances
            .Skip(processed)
            .Take(batchSize)
            .ToListAsync();
            
        if (!batch.Any()) break;
        
        // Process batch
        foreach (var workflow in batch)
        {
            await ProcessWorkflowAsync(workflow);
        }
        
        processed += batch.Count;
        
        // Force garbage collection after each batch
        if (processed % 1000 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
```

2. **Background Service Memory Management**:
```csharp
public class OutboxDispatcherService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
                
                // Process in smaller batches to reduce memory pressure
                const int batchSize = 25; // Reduced from 50
                var events = await repository.GetPendingEventsAsync(batchSize, stoppingToken);
                
                foreach (var eventBatch in events.Chunk(5)) // Process 5 at a time
                {
                    await ProcessEventBatchAsync(eventBatch, stoppingToken);
                    
                    // Small delay to prevent overwhelming the system
                    await Task.Delay(100, stoppingToken);
                }
                
                // Force cleanup after processing
                if (events.Count > 0)
                {
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processing");
            }
            
            await Task.Delay(_processingInterval, stoppingToken);
        }
    }
}
```

---

## Database-Related Problems

### 1. Deadlocks

**Symptoms**:
- "Transaction was deadlocked" errors
- Random workflow operation failures
- Performance degradation during peak usage

**Diagnosis**:
```sql
-- Enable deadlock monitoring
ALTER EVENT SESSION system_health ON SERVER STATE = START;

-- Query deadlock information
SELECT 
    r.session_id,
    r.wait_type,
    r.wait_resource,
    r.blocking_session_id,
    s.program_name,
    t.text as sql_text
FROM sys.dm_exec_requests r
JOIN sys.dm_exec_sessions s ON r.session_id = s.session_id
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.wait_type LIKE '%LOCK%';
```

**Solutions**:

1. **Transaction Scope Optimization**:
```csharp
// Minimize transaction scope
public async Task<WorkflowInstance> CompleteActivityOptimizedAsync(
    Guid workflowInstanceId, 
    string activityId, 
    Dictionary<string, object> input)
{
    // Read operations outside transaction
    var workflowInstance = await _instanceRepository.GetAsync(workflowInstanceId);
    var activityExecution = await _activityRepository.GetByActivityIdAsync(workflowInstanceId, activityId);
    
    if (workflowInstance == null || activityExecution == null)
        throw new InvalidOperationException("Workflow or activity not found");

    // Validation outside transaction
    ValidateActivityCompletion(workflowInstance, activityExecution);
    
    // Minimize transaction scope - only write operations
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Update activity execution
        activityExecution.Complete(input);
        await _activityRepository.UpdateAsync(activityExecution);
        
        // Update workflow instance
        var nextActivityId = DetermineNextActivity(workflowInstance, activityExecution);
        workflowInstance.AdvanceToActivity(nextActivityId);
        await _instanceRepository.UpdateAsync(workflowInstance);
        
        // Create execution log entry
        await _executionLogRepository.CreateAsync(new WorkflowExecutionLog
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            Event = "ActivityCompleted",
            At = DateTime.UtcNow
        });
        
        await transaction.CommitAsync();
        return workflowInstance;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

2. **Consistent Lock Ordering**:
```csharp
// Always acquire locks in consistent order to prevent deadlocks
public async Task UpdateMultipleWorkflowsAsync(List<Guid> workflowIds)
{
    // Sort IDs to ensure consistent lock ordering
    var sortedIds = workflowIds.OrderBy(id => id).ToList();
    
    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    try
    {
        foreach (var workflowId in sortedIds)
        {
            var workflow = await _instanceRepository.GetAsync(workflowId);
            if (workflow != null)
            {
                // Update workflow
                await _instanceRepository.UpdateAsync(workflow);
            }
        }
        
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### 2. Database Connection Issues

**Symptoms**:
- "Connection pool exhausted" errors
- "Timeout expired" during database operations
- Application hanging during database calls

**Diagnosis**:
```csharp
// Monitor connection pool health
public class ConnectionPoolMonitor
{
    public static void LogConnectionPoolStats(ILogger logger)
    {
        var counters = new[]
        {
            new PerformanceCounter(".NET Data Provider for SqlServer", "NumberOfPooledConnections", "WorkflowDb"),
            new PerformanceCounter(".NET Data Provider for SqlServer", "NumberOfNonPooledConnections", "WorkflowDb"),
            new PerformanceCounter(".NET Data Provider for SqlServer", "NumberOfActiveConnectionPools", "WorkflowDb")
        };
        
        foreach (var counter in counters)
        {
            logger.LogInformation("{CounterName}: {Value}", counter.CounterName, counter.NextValue());
        }
    }
}
```

**Solutions**:

1. **Connection String Optimization**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=server;Database=WorkflowDb;User Id=user;Password=pass;Max Pool Size=200;Min Pool Size=10;Connection Timeout=60;Command Timeout=120;Pooling=true;Connection Lifetime=300;"
  }
}
```

2. **Proper DbContext Management**:
```csharp
// Use DbContext factory for background services
public class OutboxDispatcherService : BackgroundService
{
    private readonly IDbContextFactory<WorkflowDbContext> _contextFactory;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create new context for each processing cycle
            await using var context = await _contextFactory.CreateDbContextAsync(stoppingToken);
            
            try
            {
                await ProcessOutboxEventsAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }
            
            await Task.Delay(_processingInterval, stoppingToken);
        }
    }
}

// Register DbContext factory
services.AddDbContextFactory<WorkflowDbContext>(options =>
    options.UseSqlServer(connectionString));
```

---

## Background Services Issues

### 1. OutboxDispatcherService Not Processing

**Symptoms**:
- Outbox events accumulating in database
- Events stuck in "Pending" status
- Real-time notifications not working

**Diagnosis**:
```sql
-- Check outbox event status
SELECT 
    Status,
    COUNT(*) as EventCount,
    MIN(OccurredAt) as OldestEvent,
    MAX(OccurredAt) as NewestEvent
FROM WorkflowOutbox
GROUP BY Status;

-- Check for poison events
SELECT TOP 10 *
FROM WorkflowOutbox
WHERE Status = 'Failed' 
  AND Attempts >= 5
ORDER BY OccurredAt DESC;
```

**Solutions**:

1. **Service Health Verification**:
```csharp
// Add health check for outbox dispatcher
public class OutboxDispatcherHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
        
        var pendingEvents = await repository.GetPendingEventsCountAsync(cancellationToken);
        var failedEvents = await repository.GetFailedEventsCountAsync(cancellationToken);
        var oldestPendingEvent = await repository.GetOldestPendingEventAsync(cancellationToken);
        
        var data = new Dictionary<string, object>
        {
            ["PendingEvents"] = pendingEvents,
            ["FailedEvents"] = failedEvents,
            ["OldestPendingAge"] = oldestPendingEvent?.OccurredAt.ToString() ?? "None"
        };
        
        if (pendingEvents > 1000)
        {
            return HealthCheckResult.Unhealthy("Too many pending outbox events", data: data);
        }
        
        if (oldestPendingEvent != null && 
            DateTime.UtcNow - oldestPendingEvent.OccurredAt > TimeSpan.FromMinutes(30))
        {
            return HealthCheckResult.Degraded("Old pending events detected", data: data);
        }
        
        return HealthCheckResult.Healthy("Outbox processing normally", data: data);
    }
}
```

2. **Manual Event Processing**:
```csharp
// Emergency outbox cleanup tool
public class OutboxRecoveryTool
{
    public async Task ReprocessFailedEventsAsync(int batchSize = 50)
    {
        var failedEvents = await _outboxRepository.GetFailedEventsAsync(batchSize);
        
        foreach (var eventToRetry in failedEvents)
        {
            try
            {
                // Reset attempts for genuinely failed events
                if (eventToRetry.Attempts >= 5 && 
                    DateTime.UtcNow - eventToRetry.OccurredAt > TimeSpan.FromHours(1))
                {
                    // Reset for manual retry
                    eventToRetry.ScheduleRetry(TimeSpan.Zero);
                    await _outboxRepository.UpdateAsync(eventToRetry);
                    
                    _logger.LogInformation("Reset failed event {EventId} for retry", eventToRetry.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset event {EventId}", eventToRetry.Id);
            }
        }
    }
    
    public async Task MoveEventsToDeadLetterAsync(TimeSpan olderThan)
    {
        var cutoffDate = DateTime.UtcNow - olderThan;
        var staleFailedEvents = await _outboxRepository.GetFailedEventsOlderThanAsync(cutoffDate);
        
        foreach (var staleEvent in staleFailedEvents)
        {
            staleEvent.MarkAsDeadLetter($"Auto-moved to dead letter after {olderThan.TotalHours} hours");
            await _outboxRepository.UpdateAsync(staleEvent);
        }
        
        _logger.LogInformation("Moved {Count} stale events to dead letter", staleFailedEvents.Count);
    }
}
```

### 2. WorkflowTimerService Issues

**Symptoms**:
- Timer-based workflows not resuming
- Timeout detection not working
- Activities stuck past their due dates

**Diagnosis**:
```sql
-- Check overdue timers
SELECT 
    wb.Id,
    wb.WorkflowInstanceId,
    wb.ActivityId,
    wb.DueAt,
    DATEDIFF(MINUTE, wb.DueAt, GETUTCDATE()) as MinutesOverdue,
    wi.Name as WorkflowName
FROM WorkflowBookmark wb
JOIN WorkflowInstance wi ON wb.WorkflowInstanceId = wi.Id
WHERE wb.Type = 'Timer' 
  AND wb.IsConsumed = 0 
  AND wb.DueAt <= GETUTCDATE()
ORDER BY wb.DueAt;
```

**Solutions**:

1. **Manual Timer Processing**:
```csharp
public class TimerRecoveryTool
{
    public async Task ProcessOverdueTimersAsync()
    {
        var overdueTimers = await _bookmarkRepository.GetOverdueTimerBookmarksAsync();
        
        foreach (var timer in overdueTimers)
        {
            try
            {
                _logger.LogWarning("Processing overdue timer for workflow {WorkflowId}, activity {ActivityId}",
                    timer.WorkflowInstanceId, timer.ActivityId);
                
                // Resume the workflow with timer completion
                await _workflowService.ResumeWorkflowAsync(
                    timer.WorkflowInstanceId,
                    timer.ActivityId,
                    "System.TimerRecovery",
                    new Dictionary<string, object> 
                    { 
                        ["TimerCompleted"] = true,
                        ["CompletedAt"] = DateTime.UtcNow,
                        ["WasOverdue"] = true,
                        ["OverdueMinutes"] = (DateTime.UtcNow - timer.DueAt.Value).TotalMinutes
                    });
                
                // Mark bookmark as consumed
                timer.MarkAsConsumed();
                await _bookmarkRepository.UpdateAsync(timer);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process overdue timer {TimerId}", timer.Id);
            }
        }
    }
}
```

2. **Timer Service Health Check**:
```csharp
public class WorkflowTimerHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var overdueCount = await _bookmarkRepository.GetOverdueTimerCountAsync(cancellationToken);
        var upcomingTimers = await _bookmarkRepository.GetUpcomingTimerCountAsync(TimeSpan.FromHours(1), cancellationToken);
        
        var data = new Dictionary<string, object>
        {
            ["OverdueTimers"] = overdueCount,
            ["UpcomingTimers"] = upcomingTimers,
            ["LastProcessedAt"] = _lastProcessedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };
        
        if (overdueCount > 50)
        {
            return HealthCheckResult.Unhealthy("Too many overdue timers", data: data);
        }
        
        if (overdueCount > 10)
        {
            return HealthCheckResult.Degraded("Some overdue timers detected", data: data);
        }
        
        return HealthCheckResult.Healthy("Timer processing normal", data: data);
    }
}
```

---

## API and Integration Problems

### 1. Authentication and Authorization Issues

**Symptoms**:
- 401 Unauthorized responses
- 403 Forbidden errors for valid users
- Token validation failures

**Diagnosis**:
```csharp
// JWT token validation diagnostics
public class JwtDiagnostics
{
    public static void ValidateToken(string token, ILogger logger)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            logger.LogInformation("Token Issuer: {Issuer}", jsonToken.Issuer);
            logger.LogInformation("Token Audience: {Audience}", string.Join(", ", jsonToken.Audiences));
            logger.LogInformation("Token Expires: {Expiry}", jsonToken.ValidTo);
            logger.LogInformation("Token Claims: {Claims}", 
                string.Join(", ", jsonToken.Claims.Select(c => $"{c.Type}={c.Value}")));
                
            if (jsonToken.ValidTo < DateTime.UtcNow)
            {
                logger.LogWarning("Token has expired");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JWT token");
        }
    }
}
```

**Solutions**:

1. **Token Refresh Mechanism**:
```csharp
public class TokenRefreshService
{
    public async Task<string> RefreshTokenIfNeededAsync(string currentToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(currentToken);
        
        // Refresh if token expires within 5 minutes
        if (jsonToken.ValidTo < DateTime.UtcNow.AddMinutes(5))
        {
            return await _authService.RefreshTokenAsync(currentToken);
        }
        
        return currentToken;
    }
}
```

2. **Role-Based Access Validation**:
```csharp
public class WorkflowAuthorizationService
{
    public async Task<bool> CanCompleteActivityAsync(string userId, Guid workflowInstanceId, string activityId)
    {
        // Check if user is assigned to the activity
        var activity = await _activityRepository.GetByActivityIdAsync(workflowInstanceId, activityId);
        if (activity?.AssignedTo == userId)
            return true;
            
        // Check if user has admin role
        var userRoles = await _userService.GetUserRolesAsync(userId);
        if (userRoles.Contains("workflow-admin"))
            return true;
            
        // Check group assignments
        var userGroups = await _userGroupService.GetUserGroupsAsync(userId);
        var activityDefinition = await _workflowDefinitionService.GetActivityDefinitionAsync(activityId);
        
        return activityDefinition.RequiredRoles.Any(role => userGroups.Contains(role));
    }
}
```

### 2. API Rate Limiting Issues

**Symptoms**:
- 429 Too Many Requests responses
- Workflow operations being throttled
- Client applications timing out

**Solutions**:

1. **Rate Limit Monitoring**:
```csharp
public class RateLimitMonitor
{
    public async Task<RateLimitStatus> GetRateLimitStatusAsync(string userId)
    {
        var window = TimeSpan.FromMinutes(1);
        var windowStart = DateTime.UtcNow.Subtract(window);
        
        var recentRequests = await _requestLogRepository.GetRequestCountAsync(
            userId, windowStart, DateTime.UtcNow);
            
        var limit = await _rateLimitService.GetUserLimitAsync(userId);
        
        return new RateLimitStatus
        {
            CurrentRequests = recentRequests,
            Limit = limit,
            WindowStart = windowStart,
            WindowEnd = DateTime.UtcNow,
            ResetTime = windowStart.Add(window)
        };
    }
}
```

2. **Client-Side Rate Limiting**:
```csharp
public class RateLimitedWorkflowClient
{
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly Queue<DateTime> _requestTimes;
    private readonly int _maxRequestsPerMinute = 60;
    
    public RateLimitedWorkflowClient()
    {
        _requestSemaphore = new SemaphoreSlim(1, 1);
        _requestTimes = new Queue<DateTime>();
    }
    
    public async Task<T> ExecuteRequestAsync<T>(Func<Task<T>> request)
    {
        await _requestSemaphore.WaitAsync();
        try
        {
            // Remove old requests from sliding window
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < oneMinuteAgo)
            {
                _requestTimes.Dequeue();
            }
            
            // Check if we're at the limit
            if (_requestTimes.Count >= _maxRequestsPerMinute)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = oldestRequest.AddMinutes(1) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime);
                }
            }
            
            // Execute request
            _requestTimes.Enqueue(DateTime.UtcNow);
            return await request();
        }
        finally
        {
            _requestSemaphore.Release();
        }
    }
}
```

---

## Monitoring and Diagnostics

### 1. Performance Monitoring

**Key Metrics to Track**:
```csharp
public class WorkflowMetricsCollector
{
    private readonly IMetrics _metrics;
    
    public void RecordWorkflowStarted(string workflowType)
    {
        _metrics.CreateCounter("workflow_started_total")
            .WithTag("workflow_type", workflowType)
            .Increment();
    }
    
    public void RecordWorkflowCompleted(string workflowType, TimeSpan duration)
    {
        _metrics.CreateCounter("workflow_completed_total")
            .WithTag("workflow_type", workflowType)
            .Increment();
            
        _metrics.CreateHistogram("workflow_duration_seconds")
            .WithTag("workflow_type", workflowType)
            .Record(duration.TotalSeconds);
    }
    
    public void RecordActivityExecution(string activityType, TimeSpan duration, bool success)
    {
        _metrics.CreateCounter("activity_executed_total")
            .WithTag("activity_type", activityType)
            .WithTag("success", success.ToString().ToLower())
            .Increment();
            
        if (success)
        {
            _metrics.CreateHistogram("activity_duration_seconds")
                .WithTag("activity_type", activityType)
                .Record(duration.TotalSeconds);
        }
    }
}
```

### 2. Health Check Implementation

**Comprehensive Health Checks**:
```csharp
public class WorkflowSystemHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, object>();
        var overallHealthy = true;
        var issues = new List<string>();
        
        // Database connectivity
        try
        {
            var dbConnectionTime = await MeasureExecutionTimeAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.WorkflowInstances.CountAsync(cancellationToken);
            });
            
            checks["DatabaseConnection"] = "Healthy";
            checks["DatabaseResponseTime"] = $"{dbConnectionTime.TotalMilliseconds:F0}ms";
            
            if (dbConnectionTime.TotalSeconds > 5)
            {
                issues.Add("Database response time is slow");
                overallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            checks["DatabaseConnection"] = $"Failed: {ex.Message}";
            issues.Add("Database connection failed");
            overallHealthy = false;
        }
        
        // Background services
        var backgroundServiceHealth = await CheckBackgroundServicesHealthAsync();
        checks.Add("BackgroundServices", backgroundServiceHealth);
        
        // Workflow engine
        try
        {
            var engineHealth = await _workflowEngine.GetHealthStatusAsync();
            checks["WorkflowEngine"] = engineHealth.IsHealthy ? "Healthy" : "Degraded";
            
            if (!engineHealth.IsHealthy)
            {
                issues.AddRange(engineHealth.Issues);
                overallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            checks["WorkflowEngine"] = $"Failed: {ex.Message}";
            issues.Add("Workflow engine check failed");
            overallHealthy = false;
        }
        
        var status = overallHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        var description = overallHealthy ? "All systems healthy" : string.Join("; ", issues);
        
        return new HealthCheckResult(status, description, data: checks);
    }
}
```

---

## Maintenance Procedures

### 1. Database Maintenance

**Regular Maintenance Tasks**:
```sql
-- Weekly index maintenance
EXEC sp_MSforeachtable @command1="PRINT '?'", @command2="ALTER INDEX ALL ON ? REORGANIZE"

-- Update statistics
EXEC sp_MSforeachtable @command1="UPDATE STATISTICS ? WITH FULLSCAN"

-- Check for fragmentation
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10 
  AND ips.page_count > 1000
ORDER BY ips.avg_fragmentation_in_percent DESC;

-- Archive old completed workflows (older than 6 months)
DECLARE @ArchiveDate DATETIME = DATEADD(MONTH, -6, GETUTCDATE());

-- Move to archive table first
INSERT INTO WorkflowInstanceArchive 
SELECT * FROM WorkflowInstance 
WHERE Status = 'Completed' 
  AND CompletedOn < @ArchiveDate;

-- Delete from main table
DELETE FROM WorkflowInstance 
WHERE Status = 'Completed' 
  AND CompletedOn < @ArchiveDate;
```

### 2. Application Maintenance

**Scheduled Maintenance Tasks**:
```csharp
public class MaintenanceService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run daily at 2 AM
                var now = DateTime.Now;
                var scheduledTime = DateTime.Today.AddHours(2);
                if (now > scheduledTime)
                    scheduledTime = scheduledTime.AddDays(1);
                
                var delay = scheduledTime - now;
                await Task.Delay(delay, stoppingToken);
                
                await PerformMaintenanceTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance tasks");
            }
        }
    }
    
    private async Task PerformMaintenanceTasksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily maintenance tasks");
        
        // Clean up old execution logs
        await CleanupOldExecutionLogsAsync(cancellationToken);
        
        // Archive completed workflows
        await ArchiveCompletedWorkflowsAsync(cancellationToken);
        
        // Clean up processed outbox events
        await CleanupProcessedOutboxEventsAsync(cancellationToken);
        
        // Verify data consistency
        await VerifyDataConsistencyAsync(cancellationToken);
        
        // Generate health report
        await GenerateHealthReportAsync(cancellationToken);
        
        _logger.LogInformation("Daily maintenance tasks completed");
    }
}
```

---

## Emergency Response

### 1. System Down Procedures

**Immediate Response Checklist**:
1. Check application logs for errors
2. Verify database connectivity
3. Check background service health
4. Validate external service dependencies
5. Review recent deployments/changes

**Emergency Recovery Commands**:
```bash
# Check application status
curl -f http://localhost:5000/health || echo "Application down"

# Check database connectivity
sqlcmd -S server -d WorkflowDb -Q "SELECT COUNT(*) FROM WorkflowInstance"

# Restart background services
systemctl restart workflow-app

# Check disk space
df -h

# Check memory usage
free -h

# Check recent errors
tail -f /var/log/workflow/application.log | grep -i error
```

### 2. Data Recovery Procedures

**Backup and Restore**:
```sql
-- Create emergency backup
BACKUP DATABASE WorkflowDb 
TO DISK = 'C:\Backups\WorkflowDb_Emergency_20241208.bak'
WITH FORMAT, COMPRESSION;

-- Point-in-time restore (if needed)
RESTORE DATABASE WorkflowDb 
FROM DISK = 'C:\Backups\WorkflowDb_Emergency_20241208.bak'
WITH REPLACE, NORECOVERY;

RESTORE LOG WorkflowDb 
FROM DISK = 'C:\Backups\WorkflowDb_Log_20241208_1400.trn'
WITH STOPAT = '2024-12-08 14:30:00';
```

**Data Consistency Checks**:
```csharp
public class DataConsistencyChecker
{
    public async Task<ConsistencyReport> CheckDataConsistencyAsync()
    {
        var issues = new List<string>();
        
        // Check for orphaned activity executions
        var orphanedActivities = await _context.WorkflowActivityExecutions
            .Where(ae => !_context.WorkflowInstances.Any(wi => wi.Id == ae.WorkflowInstanceId))
            .CountAsync();
        if (orphanedActivities > 0)
            issues.Add($"{orphanedActivities} orphaned activity executions found");
        
        // Check for workflows without activities
        var workflowsWithoutActivities = await _context.WorkflowInstances
            .Where(wi => !_context.WorkflowActivityExecutions.Any(ae => ae.WorkflowInstanceId == wi.Id))
            .CountAsync();
        if (workflowsWithoutActivities > 0)
            issues.Add($"{workflowsWithoutActivities} workflows without activity executions");
        
        // Check for unconsumed bookmarks older than 30 days
        var staleBookmarks = await _context.WorkflowBookmarks
            .Where(wb => !wb.IsConsumed && wb.CreatedAt < DateTime.UtcNow.AddDays(-30))
            .CountAsync();
        if (staleBookmarks > 0)
            issues.Add($"{staleBookmarks} stale unconsumed bookmarks");
        
        return new ConsistencyReport
        {
            IsConsistent = !issues.Any(),
            Issues = issues,
            CheckedAt = DateTime.UtcNow
        };
    }
}
```

This troubleshooting guide provides comprehensive solutions for the most common issues you'll encounter with the Workflow Module. Keep it handy for quick reference during operational issues.