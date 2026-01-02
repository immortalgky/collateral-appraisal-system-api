using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

/// <summary>
/// Background service that processes timer-based bookmarks and workflow timeouts
/// Handles workflow timeout scenarios and timer-based transitions
/// </summary>
public class WorkflowTimerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowTimerService> _logger;
    private readonly WorkflowResilienceOptions _resilienceOptions;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(60); // Check every minute
    private const int BatchSize = 100;

    public WorkflowTimerService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowTimerService> logger,
        IOptions<WorkflowResilienceOptions> resilienceOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _resilienceOptions = resilienceOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow timer service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueBookmarksAsync(stoppingToken);
                await ProcessWorkflowTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing workflow timers");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Workflow timer service stopping");
    }

    private async Task ProcessDueBookmarksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var bookmarkRepository = scope.ServiceProvider.GetRequiredService<IWorkflowBookmarkRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
        var resilienceService = scope.ServiceProvider.GetRequiredService<IWorkflowResilienceService>();

        // Get due timer bookmarks
        var dueBookmarks = await bookmarkRepository.GetDueTimerBookmarksAsync(BatchSize, DateTime.UtcNow, cancellationToken);
        
        if (!dueBookmarks.Any())
        {
            _logger.LogDebug("No due timer bookmarks to process");
            return;
        }

        _logger.LogInformation("Processing {Count} due timer bookmarks", dueBookmarks.Count);

        foreach (var bookmark in dueBookmarks)
        {
            await ProcessDueBookmarkAsync(bookmark, outboxRepository, resilienceService, cancellationToken);
        }

        // Note: Individual repositories handle their own SaveChanges in their methods
    }

    private async Task ProcessDueBookmarkAsync(
        WorkflowBookmark bookmark,
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing due bookmark {BookmarkId} for workflow {WorkflowInstanceId}, activity {ActivityId}",
                bookmark.Id, bookmark.WorkflowInstanceId, bookmark.ActivityId);

            // Execute with resilience
            await resilienceService.ExecuteWithRetryAsync(
                async ct => await TriggerBookmarkTimeoutAsync(bookmark, outboxRepository, ct),
                $"timer-bookmark-{bookmark.Type}",
                cancellationToken);

            _logger.LogDebug("Successfully processed due bookmark {BookmarkId}", bookmark.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process due bookmark {BookmarkId}. Will retry on next cycle", 
                bookmark.Id);
        }
    }

    private async Task<bool> TriggerBookmarkTimeoutAsync(
        WorkflowBookmark bookmark,
        IWorkflowOutboxRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        // Consume the bookmark to prevent duplicate processing
        bookmark.Consume("SYSTEM_TIMER");

        // Create appropriate timeout event based on bookmark type
        var timeoutEvent = bookmark.Type switch
        {
            BookmarkType.Timer => CreateTimerExpiredEvent(bookmark),
            BookmarkType.UserAction => CreateUserActionTimeoutEvent(bookmark),
            BookmarkType.ExternalMessage => CreateExternalMessageTimeoutEvent(bookmark),
            _ => CreateGenericTimeoutEvent(bookmark)
        };

        await outboxRepository.AddAsync(timeoutEvent, cancellationToken);
        
        _logger.LogInformation("Created timeout event for bookmark {BookmarkId} of type {BookmarkType}",
            bookmark.Id, bookmark.Type);

        return true;
    }

    private WorkflowOutbox CreateTimerExpiredEvent(WorkflowBookmark bookmark)
    {
        return WorkflowOutbox.Create(
            "TimerExpired",
            JsonSerializer.Serialize(new
            {
                WorkflowInstanceId = bookmark.WorkflowInstanceId,
                ActivityId = bookmark.ActivityId,
                BookmarkKey = bookmark.Key,
                ExpiredAt = DateTime.UtcNow,
                BookmarkPayload = bookmark.Payload
            }),
            correlationId: null, // Will be filled by repository if needed
            workflowInstanceId: bookmark.WorkflowInstanceId,
            activityId: bookmark.ActivityId
        );
    }

    private WorkflowOutbox CreateUserActionTimeoutEvent(WorkflowBookmark bookmark)
    {
        return WorkflowOutbox.Create(
            "UserActionTimeout",
            JsonSerializer.Serialize(new
            {
                WorkflowInstanceId = bookmark.WorkflowInstanceId,
                ActivityId = bookmark.ActivityId,
                BookmarkKey = bookmark.Key,
                TimeoutAt = DateTime.UtcNow,
                ExpectedAction = bookmark.Payload
            }),
            correlationId: null,
            workflowInstanceId: bookmark.WorkflowInstanceId,
            activityId: bookmark.ActivityId
        );
    }

    private WorkflowOutbox CreateExternalMessageTimeoutEvent(WorkflowBookmark bookmark)
    {
        return WorkflowOutbox.Create(
            "ExternalMessageTimeout",
            JsonSerializer.Serialize(new
            {
                WorkflowInstanceId = bookmark.WorkflowInstanceId,
                ActivityId = bookmark.ActivityId,
                BookmarkKey = bookmark.Key,
                TimeoutAt = DateTime.UtcNow,
                ExpectedMessage = bookmark.Payload
            }),
            correlationId: null,
            workflowInstanceId: bookmark.WorkflowInstanceId,
            activityId: bookmark.ActivityId
        );
    }

    private WorkflowOutbox CreateGenericTimeoutEvent(WorkflowBookmark bookmark)
    {
        return WorkflowOutbox.Create(
            "BookmarkTimeout",
            JsonSerializer.Serialize(new
            {
                WorkflowInstanceId = bookmark.WorkflowInstanceId,
                ActivityId = bookmark.ActivityId,
                BookmarkKey = bookmark.Key,
                BookmarkType = bookmark.Type.ToString(),
                TimeoutAt = DateTime.UtcNow,
                BookmarkData = bookmark.Payload
            }),
            correlationId: null,
            workflowInstanceId: bookmark.WorkflowInstanceId,
            activityId: bookmark.ActivityId
        );
    }

    private async Task ProcessWorkflowTimeoutsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var workflowRepository = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
        var resilienceService = scope.ServiceProvider.GetRequiredService<IWorkflowResilienceService>();

        // Find workflows that have been running too long
        var longRunningWorkflows = await workflowRepository.GetLongRunningWorkflowsAsync(
            TimeSpan.FromHours(24), // Configurable timeout threshold
            BatchSize,
            cancellationToken);

        if (!longRunningWorkflows.Any())
        {
            _logger.LogDebug("No long-running workflows found");
            return;
        }

        _logger.LogInformation("Processing {Count} potentially timed-out workflows", longRunningWorkflows.Count());

        foreach (var workflow in longRunningWorkflows)
        {
            await ProcessWorkflowTimeoutAsync(workflow, outboxRepository, resilienceService, cancellationToken);
        }

        // Note: Individual repositories handle their own SaveChanges in their methods
    }

    private async Task ProcessWorkflowTimeoutAsync(
        WorkflowInstance workflow,
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning("Processing potential timeout for workflow {WorkflowInstanceId} running since {StartedAt}",
                workflow.Id, workflow.CreatedOn);

            await resilienceService.ExecuteWithRetryAsync(
                async ct => await HandleWorkflowTimeoutAsync(workflow, outboxRepository, ct),
                "workflow-timeout",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process workflow timeout for {WorkflowInstanceId}",
                workflow.Id);
        }
    }

    private async Task<bool> HandleWorkflowTimeoutAsync(
        WorkflowInstance workflow,
        IWorkflowOutboxRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        // Check if workflow is still active and should be timed out
        if (workflow.Status != WorkflowStatus.Running)
        {
            return true; // Already handled
        }

        // For now, we'll create a warning event rather than automatically suspending
        // In a production system, you might want more sophisticated timeout handling
        var timeoutWarningEvent = WorkflowOutbox.Create(
            "WorkflowTimeoutWarning",
            JsonSerializer.Serialize(new
            {
                WorkflowInstanceId = workflow.Id,
                StartedAt = workflow.CreatedOn,
                RunningDuration = DateTime.UtcNow - workflow.CreatedOn,
                CurrentStatus = workflow.Status.ToString(),
                WarningAt = DateTime.UtcNow
            }),
            correlationId: workflow.CorrelationId,
            workflowInstanceId: workflow.Id
        );

        await outboxRepository.AddAsync(timeoutWarningEvent, cancellationToken);

        _logger.LogWarning("Created timeout warning for long-running workflow {WorkflowInstanceId}",
            workflow.Id);

        return true;
    }
}