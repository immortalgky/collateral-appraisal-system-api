using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

/// <summary>
/// Background service for performing workflow maintenance and cleanup operations
/// Handles cleanup of old events, expired bookmarks, and maintenance of workflow data
/// </summary>
public class WorkflowCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowCleanupService> _logger;
    private readonly WorkflowResilienceOptions _resilienceOptions;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run cleanup every 6 hours
    
    // Retention periods
    private readonly TimeSpan _processedEventRetention = TimeSpan.FromDays(30);
    private readonly TimeSpan _expiredBookmarkRetention = TimeSpan.FromDays(7);
    private readonly TimeSpan _executionLogRetention = TimeSpan.FromDays(90);

    public WorkflowCleanupService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowCleanupService> logger,
        IOptions<WorkflowResilienceOptions> resilienceOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _resilienceOptions = resilienceOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow cleanup service starting");

        // Add initial delay to avoid startup contention
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupOperationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during workflow cleanup operations");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Workflow cleanup service stopping");
    }

    private async Task PerformCleanupOperationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
        var bookmarkRepository = scope.ServiceProvider.GetRequiredService<IWorkflowBookmarkRepository>();
        var executionLogRepository = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionLogRepository>();
        var resilienceService = scope.ServiceProvider.GetRequiredService<IWorkflowResilienceService>();

        _logger.LogInformation("Starting workflow cleanup operations");

        var cleanupTasks = new[]
        {
            CleanupProcessedOutboxEventsAsync(outboxRepository, resilienceService, cancellationToken),
            CleanupExpiredBookmarksAsync(bookmarkRepository, resilienceService, cancellationToken),
            CleanupOldExecutionLogsAsync(executionLogRepository, resilienceService, cancellationToken),
            MoveFailedEventsToDeadLetterAsync(outboxRepository, resilienceService, cancellationToken)
        };

        // Execute all cleanup tasks concurrently
        await Task.WhenAll(cleanupTasks);

        _logger.LogInformation("Completed workflow cleanup operations");
    }

    private async Task CleanupProcessedOutboxEventsAsync(
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var cleanedCount = await resilienceService.ExecuteWithRetryAsync(
                async ct => await outboxRepository.CleanupProcessedEventsAsync(_processedEventRetention, ct),
                "cleanup-outbox-events",
                cancellationToken);

            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} processed outbox events older than {Retention} days",
                    cleanedCount, _processedEventRetention.TotalDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup processed outbox events");
        }
    }

    private async Task CleanupExpiredBookmarksAsync(
        IWorkflowBookmarkRepository bookmarkRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var cleanedCount = await resilienceService.ExecuteWithRetryAsync(
                async ct => await bookmarkRepository.CleanupExpiredBookmarksAsync(_expiredBookmarkRetention, ct),
                "cleanup-expired-bookmarks",
                cancellationToken);

            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired bookmarks older than {Retention} days",
                    cleanedCount, _expiredBookmarkRetention.TotalDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired bookmarks");
        }
    }

    private async Task CleanupOldExecutionLogsAsync(
        IWorkflowExecutionLogRepository executionLogRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var cleanedCount = await resilienceService.ExecuteWithRetryAsync(
                async ct => await executionLogRepository.CleanupOldLogsAsync(_executionLogRetention, ct),
                "cleanup-execution-logs",
                cancellationToken);

            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} execution logs older than {Retention} days",
                    cleanedCount, _executionLogRetention.TotalDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old execution logs");
        }
    }

    private async Task MoveFailedEventsToDeadLetterAsync(
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            const int maxRetries = 5;
            var movedCount = await resilienceService.ExecuteWithRetryAsync(
                async ct => await outboxRepository.MoveToDeadLetterAsync(maxRetries, ct),
                "move-to-dead-letter",
                cancellationToken);

            if (movedCount > 0)
            {
                _logger.LogWarning("Moved {Count} events to dead letter queue after {MaxRetries} failed attempts",
                    movedCount, maxRetries);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move events to dead letter queue");
        }
    }
}