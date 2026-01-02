using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Workflow.Data;
using Workflow.Workflow.Configuration;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Services;

/// <summary>
/// Background service that processes outbox events for reliable event publishing
/// Implements the outbox pattern to ensure events are published exactly once
/// </summary>
public class OutboxDispatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcherService> _logger;
    private readonly WorkflowResilienceOptions _resilienceOptions;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;
    private const int MaxRetryAttempts = 5;

    public OutboxDispatcherService(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcherService> logger,
        IOptions<WorkflowResilienceOptions> resilienceOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _resilienceOptions = resilienceOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox dispatcher service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox dispatcher service stopping");
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IWorkflowOutboxRepository>();
        var resilienceService = scope.ServiceProvider.GetRequiredService<IWorkflowResilienceService>();

        // Get pending outbox events
        var pendingEvents = await outboxRepository.GetPendingEventsAsync(BatchSize, cancellationToken);
        
        if (!pendingEvents.Any())
        {
            _logger.LogDebug("No pending outbox events to process");
            return;
        }

        _logger.LogInformation("Processing {Count} outbox events", pendingEvents.Count);

        foreach (var outboxEvent in pendingEvents)
        {
            await ProcessSingleEventAsync(outboxEvent, resilienceService, cancellationToken);
        }

        // Note: Repository handles SaveChanges in individual methods
    }

    private async Task ProcessSingleEventAsync(
        WorkflowOutbox outboxEvent, 
        IWorkflowResilienceService resilienceService,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing outbox event {EventId} of type {EventType}", 
                outboxEvent.Id, outboxEvent.Type);

            // Execute event publishing with resilience
            await resilienceService.ExecuteWithRetryAsync(
                async ct => await PublishEventAsync(outboxEvent, ct),
                $"outbox-{outboxEvent.Type}",
                cancellationToken);

            // Mark as processed
            outboxEvent.MarkAsProcessed();
            
            _logger.LogDebug("Successfully processed outbox event {EventId}", outboxEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process outbox event {EventId} after retries. Attempt {AttemptCount}", 
                outboxEvent.Id, outboxEvent.Attempts + 1);

            // Increment attempt count and check if we should mark as dead letter
            outboxEvent.IncrementAttempt(ex.Message);

            if (outboxEvent.Attempts >= MaxRetryAttempts)
            {
                _logger.LogError("Moving outbox event {EventId} to dead letter queue after {MaxAttempts} failed attempts",
                    outboxEvent.Id, MaxRetryAttempts);
                outboxEvent.MarkAsDeadLetter($"Failed after {MaxRetryAttempts} attempts: {ex.Message}");
            }
        }
    }

    private async Task<bool> PublishEventAsync(WorkflowOutbox outboxEvent, CancellationToken cancellationToken)
    {
        // Parse the event data
        var eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(outboxEvent.Payload);
        
        // Route to appropriate event publisher based on event type
        return outboxEvent.Type switch
        {
            "WorkflowStarted" => await PublishWorkflowStartedEventAsync(eventData, outboxEvent, cancellationToken),
            "WorkflowCompleted" => await PublishWorkflowCompletedEventAsync(eventData, outboxEvent, cancellationToken),
            "WorkflowFailed" => await PublishWorkflowFailedEventAsync(eventData, outboxEvent, cancellationToken),
            "WorkflowSuspended" => await PublishWorkflowSuspendedEventAsync(eventData, outboxEvent, cancellationToken),
            "WorkflowActivityCompleted" => await PublishActivityCompletedEventAsync(eventData, outboxEvent, cancellationToken),
            "WorkflowActivityFailed" => await PublishActivityFailedEventAsync(eventData, outboxEvent, cancellationToken),
            "ExternalCallCompleted" => await PublishExternalCallCompletedEventAsync(eventData, outboxEvent, cancellationToken),
            "ExternalCallFailed" => await PublishExternalCallFailedEventAsync(eventData, outboxEvent, cancellationToken),
            _ => await PublishGenericEventAsync(eventData, outboxEvent, cancellationToken)
        };
    }

    private async Task<bool> PublishWorkflowStartedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowStarted event for workflow {WorkflowInstanceId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"));

        // TODO: Integrate with actual event publishing system (SignalR, Service Bus, etc.)
        // For now, we'll simulate publishing
        await Task.Delay(100, cancellationToken);
        
        return true;
    }

    private async Task<bool> PublishWorkflowCompletedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowCompleted event for workflow {WorkflowInstanceId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishWorkflowFailedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowFailed event for workflow {WorkflowInstanceId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishWorkflowSuspendedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowSuspended event for workflow {WorkflowInstanceId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishActivityCompletedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowActivityCompleted event for workflow {WorkflowInstanceId}, activity {ActivityId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"),
            eventData.GetValueOrDefault("ActivityId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishActivityFailedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing WorkflowActivityFailed event for workflow {WorkflowInstanceId}, activity {ActivityId}",
            eventData.GetValueOrDefault("WorkflowInstanceId"),
            eventData.GetValueOrDefault("ActivityId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishExternalCallCompletedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing ExternalCallCompleted event for call {ExternalCallId}",
            eventData.GetValueOrDefault("ExternalCallId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishExternalCallFailedEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing ExternalCallFailed event for call {ExternalCallId}",
            eventData.GetValueOrDefault("ExternalCallId"));

        await Task.Delay(100, cancellationToken);
        return true;
    }

    private async Task<bool> PublishGenericEventAsync(
        Dictionary<string, object> eventData, 
        WorkflowOutbox outboxEvent, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing generic event {EventType}", outboxEvent.Type);

        await Task.Delay(100, cancellationToken);
        return true;
    }
}