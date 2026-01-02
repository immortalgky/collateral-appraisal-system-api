using Workflow.Workflow.Engine;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Core;

namespace Workflow.Workflow.Services;

/// <summary>
/// Workflow service - handles orchestration, validation, persistence, and events with resilience
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowOrchestrator _orchestrator; // ENHANCED: Step-by-step orchestration
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly IWorkflowFaultHandler _faultHandler;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IWorkflowEngine workflowEngine,
        IWorkflowOrchestrator orchestrator,
        IWorkflowPersistenceService persistenceService,
        IWorkflowEventPublisher eventPublisher,
        IWorkflowResilienceService resilienceService,
        IWorkflowFaultHandler faultHandler,
        ILogger<WorkflowService> logger)
    {
        _workflowEngine = workflowEngine;
        _orchestrator = orchestrator;
        _persistenceService = persistenceService;
        _eventPublisher = eventPublisher;
        _resilienceService = resilienceService;
        _faultHandler = faultHandler;
        _logger = logger;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? assignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        int attemptNumber = 1;
        const int maxAttempts = 3;

        while (attemptNumber <= maxAttempts)
        {
            try
            {
                _logger.LogInformation("SERVICE: Starting workflow for definition {WorkflowDefinitionId} (attempt {AttemptNumber})",
                    workflowDefinitionId, attemptNumber);

                // Execute with resilience patterns
                return await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
                {
                    // 1. ENHANCED: DELEGATE to WorkflowEngine for single-step startup, then orchestrator for remaining steps
                    var startupResult = await _workflowEngine.StartWorkflowAsync(
                        workflowDefinitionId, instanceName, startedBy, initialVariables, correlationId, assignmentOverrides, ct);

                    // 2. ENHANCED: If workflow needs more steps, use orchestrator for step-by-step execution
                    var executionResult = startupResult.Status == WorkflowExecutionStatus.StepCompleted
                        ? await _orchestrator.ExecuteCompleteWorkflowAsync(startupResult.WorkflowInstance!.Id, 100, ct)
                        : startupResult;

                    if (executionResult.Status == WorkflowExecutionStatus.Failed)
                        throw new InvalidOperationException(executionResult.ErrorMessage ?? "Workflow startup failed");

                    if (executionResult.WorkflowInstance == null)
                        throw new InvalidOperationException("WorkflowEngine returned null instance");

                    // 2. PUBLISH EVENTS via EventPublisher (with resilience)
                    await _resilienceService.ExecuteWithRetryAsync(async ct2 =>
                    {
                        await _eventPublisher.PublishWorkflowStartedAsync(
                            executionResult.WorkflowInstance.Id,
                            workflowDefinitionId,
                            instanceName,
                            startedBy,
                            executionResult.WorkflowInstance.StartedOn,
                            correlationId,
                            ct2);
                        return true; // Return type required for ExecuteWithRetryAsync
                    }, $"publish-workflow-started-{executionResult.WorkflowInstance.Id}", ct);

                    _logger.LogInformation("SERVICE: Successfully started workflow instance {WorkflowInstanceId}",
                        executionResult.WorkflowInstance.Id);

                    return executionResult.WorkflowInstance;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SERVICE: Workflow startup attempt {AttemptNumber} failed for definition {WorkflowDefinitionId}",
                    attemptNumber, workflowDefinitionId);

                // Create fault context
                var faultContext = new StartWorkflowFaultContext(
                    workflowDefinitionId,
                    instanceName,
                    startedBy,
                    ex,
                    attemptNumber);

                // Handle the fault
                var faultResult = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext, cancellationToken);

                if (!faultResult.ShouldRetry || attemptNumber >= maxAttempts)
                {
                    _logger.LogError(ex, "SERVICE: Workflow startup failed permanently after {AttemptNumber} attempts. Reason: {FailureReason}",
                        attemptNumber, faultResult.RecommendedAction);
                    throw;
                }

                if (faultResult.RetryDelay.HasValue)
                {
                    _logger.LogInformation("SERVICE: Retrying workflow startup in {RetryDelay}ms", 
                        faultResult.RetryDelay.Value.TotalMilliseconds);
                    await Task.Delay(faultResult.RetryDelay.Value, cancellationToken);
                }

                attemptNumber++;
            }
        }

        throw new InvalidOperationException($"Workflow startup failed after {maxAttempts} attempts");
    }

    public async Task<WorkflowInstance> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("SERVICE: Resuming workflow {WorkflowInstanceId} at activity {ActivityId}",
                workflowInstanceId, activityId);

            // 1. ENHANCED: Use orchestrator for step-by-step resume execution
            var executionResult = await _orchestrator.ContinueWorkflowExecutionAsync(
                workflowInstanceId, activityId, input, 100, cancellationToken);

            if (executionResult.Status == WorkflowExecutionStatus.Failed)
                throw new InvalidOperationException(executionResult.ErrorMessage ?? "Workflow resume failed");

            if (executionResult.WorkflowInstance == null)
                throw new InvalidOperationException("WorkflowEngine returned null instance");

            // 2. PUBLISH EVENTS via EventPublisher
            await _eventPublisher.PublishActivityCompletedAsync(
                workflowInstanceId,
                activityId,
                completedBy,
                DateTime.Now,
                input,
                input?.TryGetValue("comments", out var commentsValue) == true ? commentsValue?.ToString() : null,
                cancellationToken);

            // If workflow completed, publish the completion event
            if (executionResult.Status == WorkflowExecutionStatus.Completed)
                await _eventPublisher.PublishWorkflowCompletedAsync(
                    workflowInstanceId,
                    completedBy,
                    DateTime.Now,
                    cancellationToken);

            _logger.LogInformation("SERVICE: Successfully resumed workflow {WorkflowInstanceId} with status {Status}",
                workflowInstanceId, executionResult.Status);

            return executionResult.WorkflowInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SERVICE: Failed to resume workflow {WorkflowInstanceId}", workflowInstanceId);
            throw;
        }
    }

    public async Task CancelWorkflowAsync(
        Guid workflowInstanceId,
        string cancelledBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var workflowInstance =
            await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null)
            throw new InvalidOperationException($"Workflow instance not found: {workflowInstanceId}");

        workflowInstance.UpdateStatus(WorkflowStatus.Cancelled, reason);

        // Cancel any current activity execution
        var currentActivityExecution =
            await _persistenceService.GetCurrentActivityExecutionAsync(workflowInstanceId, cancellationToken);
        if (currentActivityExecution != null)
        {
            currentActivityExecution.Cancel(reason);
            await _persistenceService.UpdateActivityExecutionAsync(currentActivityExecution, cancellationToken);
        }

        await _persistenceService.SaveWorkflowInstanceAsync(workflowInstance, cancellationToken);

        await _eventPublisher.PublishWorkflowCancelledAsync(
            workflowInstanceId,
            cancelledBy,
            DateTime.Now,
            reason,
            cancellationToken);

        _logger.LogInformation("Workflow instance {WorkflowInstanceId} cancelled by {CancelledBy}: {Reason}",
            workflowInstanceId, cancelledBy, reason);
    }

    public async Task<WorkflowInstance?> GetWorkflowInstanceAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetWorkflowInstanceWithExecutionsAsync(workflowInstanceId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetUserTasksAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetUserTasksAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetCurrentActivitiesForUserAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityExecution>> GetCurrentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _persistenceService.GetCurrentActivitiesAsync(cancellationToken);
    }

    public async Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default)
    {
        return await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema, cancellationToken);
    }
}