using MediatR;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Commands;

/// <summary>
/// Handles workflow cancellation with proper state management and event publishing
/// </summary>
public sealed class CancelWorkflowCommandHandler : IRequestHandler<CancelWorkflowCommand, CancelWorkflowResult>
{
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowOutboxRepository _outboxRepository;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly IWorkflowFaultHandler _faultHandler;
    private readonly ILogger<CancelWorkflowCommandHandler> _logger;

    public CancelWorkflowCommandHandler(
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowResilienceService resilienceService,
        IWorkflowFaultHandler faultHandler,
        ILogger<CancelWorkflowCommandHandler> logger)
    {
        _workflowInstanceRepository = workflowInstanceRepository;
        _outboxRepository = outboxRepository;
        _resilienceService = resilienceService;
        _faultHandler = faultHandler;
        _logger = logger;
    }

    public async Task<CancelWorkflowResult> Handle(CancelWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("COMMAND: Cancelling workflow {WorkflowInstanceId} by {CancelledBy}",
                request.WorkflowInstanceId, request.CancelledBy);

            // Execute with resilience for database operations
            return await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
            {
                // 1. Load workflow instance
                var workflowInstance = await _workflowInstanceRepository.GetByIdAsync(request.WorkflowInstanceId, ct);
                if (workflowInstance == null)
                {
                    _logger.LogWarning("COMMAND: Workflow instance {WorkflowInstanceId} not found for cancellation",
                        request.WorkflowInstanceId);
                    return new CancelWorkflowResult(false, null, "Workflow instance not found");
                }

                // 2. Validate workflow can be cancelled
                if (workflowInstance.Status == WorkflowStatus.Completed ||
                    workflowInstance.Status == WorkflowStatus.Cancelled)
                {
                    _logger.LogWarning("COMMAND: Cannot cancel workflow {WorkflowInstanceId} in status {Status}",
                        request.WorkflowInstanceId, workflowInstance.Status);
                    return new CancelWorkflowResult(false, workflowInstance,
                        $"Cannot cancel workflow in {workflowInstance.Status} status");
                }

                // 3. Update workflow status with cancellation details
                workflowInstance.UpdateStatus(WorkflowStatus.Cancelled, request.Reason ?? "Workflow cancelled");

                // 4. Attempt optimistic concurrency update
                var maxRetries = 3;
                int attemptCount = 0;
                bool updateSuccessful = false;

                while (attemptCount < maxRetries && !updateSuccessful)
                {
                    try
                    {
                        updateSuccessful = await _workflowInstanceRepository.TryUpdateWithConcurrencyAsync(
                            workflowInstance, ct);

                        if (!updateSuccessful)
                        {
                            attemptCount++;
                            if (attemptCount < maxRetries)
                            {
                                _logger.LogWarning("COMMAND: Concurrency conflict cancelling workflow {WorkflowInstanceId}, attempt {Attempt}/{MaxRetries}",
                                    request.WorkflowInstanceId, attemptCount, maxRetries);

                                // Reload and reapply changes
                                var latestInstance = await _workflowInstanceRepository.GetByIdAsync(request.WorkflowInstanceId, ct);
                                if (latestInstance == null)
                                {
                                    return new CancelWorkflowResult(false, null, "Workflow instance not found during retry");
                                }

                                // Validate again after reload
                                if (latestInstance.Status == WorkflowStatus.Completed ||
                                    latestInstance.Status == WorkflowStatus.Cancelled)
                                {
                                    return new CancelWorkflowResult(true, latestInstance, "Workflow already completed/cancelled");
                                }

                                latestInstance.UpdateStatus(WorkflowStatus.Cancelled, request.Reason ?? "Workflow cancelled");
                                workflowInstance = latestInstance;

                                await Task.Delay(TimeSpan.FromMilliseconds(100 * attemptCount), ct);
                            }
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "COMMAND: Error updating workflow {WorkflowInstanceId} during cancellation",
                            request.WorkflowInstanceId);
                        return new CancelWorkflowResult(false, workflowInstance, 
                            $"Failed to update workflow: {updateEx.Message}");
                    }
                }

                if (!updateSuccessful)
                {
                    _logger.LogError("COMMAND: Failed to cancel workflow {WorkflowInstanceId} after {MaxRetries} attempts due to concurrency conflicts",
                        request.WorkflowInstanceId, maxRetries);
                    return new CancelWorkflowResult(false, workflowInstance,
                        "Failed to cancel workflow due to concurrency conflicts");
                }

                // 5. Write cancellation event to outbox
                await WriteWorkflowCancelledEventAsync(workflowInstance, request.CancelledBy, request.Reason, ct);

                _logger.LogInformation("COMMAND: Successfully cancelled workflow {WorkflowInstanceId}",
                    request.WorkflowInstanceId);

                return new CancelWorkflowResult(true, workflowInstance);

            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COMMAND: Critical error cancelling workflow {WorkflowInstanceId}",
                request.WorkflowInstanceId);

            return new CancelWorkflowResult(false, null, $"Critical error during cancellation: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes workflow cancelled event to outbox for reliable publishing
    /// </summary>
    private async Task WriteWorkflowCancelledEventAsync(
        WorkflowInstance workflowInstance,
        string cancelledBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        var eventData = new
        {
            WorkflowInstanceId = workflowInstance.Id,
            CancelledBy = cancelledBy,
            Reason = reason ?? "No reason provided",
            CancelledAt = DateTime.UtcNow,
            CorrelationId = workflowInstance.CorrelationId,
            CurrentActivityId = workflowInstance.CurrentActivityId
        };

        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowCancelled",
            System.Text.Json.JsonSerializer.Serialize(eventData),
            new Dictionary<string, string>
            {
                ["WorkflowInstanceId"] = workflowInstance.Id.ToString(),
                ["CancelledBy"] = cancelledBy,
                ["CorrelationId"] = workflowInstance.CorrelationId ?? "",
                ["CurrentActivityId"] = workflowInstance.CurrentActivityId ?? ""
            }
        );

        await _outboxRepository.AddAsync(outboxEvent, cancellationToken);

        _logger.LogDebug("COMMAND: Queued WorkflowCancelled event for workflow {WorkflowId}",
            workflowInstance.Id);
    }
}