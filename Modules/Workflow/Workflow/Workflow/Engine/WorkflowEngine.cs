using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using Workflow.Telemetry;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Core workflow engine - Orchestration responsibilities
/// Coordinates workflow execution, manages activity lifecycle, and handles execution flow
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowActivityFactory _activityFactory;
    private readonly IFlowControlManager _flowControlManager;
    private readonly IWorkflowLifecycleManager _lifecycleManager;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowStateManager _stateManager;
    private readonly IWorkflowResilienceService _resilienceService;
    private readonly IWorkflowFaultHandler _faultHandler;
    private readonly IWorkflowBookmarkService _bookmarkService;
    private readonly IWorkflowOutboxRepository _outboxRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly IWorkflowLogger _workflowLogger;
    private readonly IWorkflowMetrics _workflowMetrics;
    private readonly IWorkflowTracing _workflowTracing;

    public WorkflowEngine(
        IWorkflowActivityFactory activityFactory,
        IFlowControlManager flowControlManager,
        IWorkflowLifecycleManager lifecycleManager,
        IWorkflowPersistenceService persistenceService,
        IWorkflowStateManager stateManager,
        IWorkflowResilienceService resilienceService,
        IWorkflowFaultHandler faultHandler,
        IWorkflowBookmarkService bookmarkService,
        IWorkflowOutboxRepository outboxRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        ILogger<WorkflowEngine> logger,
        IWorkflowLogger workflowLogger,
        IWorkflowMetrics workflowMetrics,
        IWorkflowTracing workflowTracing)
    {
        _activityFactory = activityFactory;
        _flowControlManager = flowControlManager;
        _lifecycleManager = lifecycleManager;
        _persistenceService = persistenceService;
        _stateManager = stateManager;
        _resilienceService = resilienceService;
        _faultHandler = faultHandler;
        _bookmarkService = bookmarkService;
        _outboxRepository = outboxRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _logger = logger;
        _workflowLogger = workflowLogger;
        _workflowMetrics = workflowMetrics;
        _workflowTracing = workflowTracing;
    }

    public async Task<WorkflowExecutionResult> StartWorkflowAsync(
        Guid workflowDefinitionId,
        string instanceName,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        string? correlationId = null,
        Dictionary<string, RuntimeOverride>? assignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        // PHASE 4: Create root distributed tracing span for workflow startup
        return await _workflowTracing.TraceWorkflowOperationAsync(
            WorkflowTelemetryConstants.ActivityNames.WorkflowStart,
            Guid.Empty, // Will be set when instance is created
            workflowDefinitionId,
            async (span) =>
            {
                // ENHANCED: Use structured logging with correlation scope for comprehensive observability
                using var correlationScope = _workflowLogger.CreateCorrelationScope(Guid.NewGuid(), correlationId, "StartWorkflow");
        
        try
        {
            // BEFORE: Basic logging with limited context
            // _logger.LogInformation("ORCHESTRATION: Starting workflow for definition {WorkflowDefinitionId}, correlationId {CorrelationId}", workflowDefinitionId, correlationId ?? "N/A");
            
            // AFTER: Enhanced structured logging with correlation context and telemetry integration
            _workflowLogger.LogWorkflowStarting(workflowDefinitionId, instanceName, startedBy, correlationId);

            // 1. Load workflow schema via persistence service
            var workflowSchema =
                await _persistenceService.GetWorkflowSchemaAsync(workflowDefinitionId, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(null, $"Workflow definition not found: {workflowDefinitionId}");

            // 2. Initialize a workflow instance via lifecycle manager
            var workflowInstance = _lifecycleManager.InitializeWorkflowAsync(
                workflowDefinitionId, workflowSchema, instanceName, startedBy, initialVariables, correlationId,
                assignmentOverrides, cancellationToken);

            // PHASE 4: Update tracing span with workflow instance ID now that it's created
            span.SetWorkflowInstanceId(workflowInstance.Id);

            // PHASE 3: Record workflow started metric for observability
            // BEFORE: No metrics were recorded
            // AFTER: Comprehensive metrics with semantic tags for monitoring and alerting
            _workflowMetrics.RecordWorkflowStarted(
                workflowSchema.Name ?? "Unknown",
                workflowDefinitionId.ToString(),
                new KeyValuePair<string, object?>("started_by", startedBy),
                new KeyValuePair<string, object?>("has_correlation_id", !string.IsNullOrEmpty(correlationId)),
                new KeyValuePair<string, object?>("has_assignment_overrides", assignmentOverrides?.Count > 0));

            // 3. Get start activity or 1st activity if none is specified
            var startActivity = _flowControlManager.GetStartActivity(workflowSchema);

            // ENHANCED: Log workflow started event with full context
            _workflowLogger.LogWorkflowStarted(workflowInstance);

            // 4. Execute the workflow from the start activity
            var executionResult =
                await ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity, null, false,
                    cancellationToken);

            // BEFORE: Basic completion logging
            // _logger.LogInformation("ORCHESTRATION: Workflow startup finished with status {Status}", executionResult.Status);
            
            // AFTER: Enhanced logging and metrics based on execution result
            if (executionResult.Status == WorkflowExecutionStatus.Completed)
            {
                var totalDuration = DateTime.UtcNow - (workflowInstance.CreatedOn ?? workflowInstance.StartedOn);
                _workflowLogger.LogWorkflowCompleted(workflowInstance, totalDuration);
                
                // PHASE 3: Record workflow completion metrics
                // BEFORE: No metrics for workflow completion
                // AFTER: Comprehensive completion metrics with duration and status
                _workflowMetrics.RecordWorkflowCompleted(
                    workflowSchema.Name ?? "Unknown",
                    workflowDefinitionId.ToString(),
                    executionResult.Status.ToString(),
                    new KeyValuePair<string, object?>("duration_ms", totalDuration.TotalMilliseconds));
                    
                _workflowMetrics.RecordWorkflowDuration(
                    workflowSchema.Name ?? "Unknown",
                    workflowDefinitionId.ToString(),
                    totalDuration,
                    executionResult.Status.ToString());
            }
            else if (executionResult.Status == WorkflowExecutionStatus.StepCompleted)
            {
                _workflowLogger.LogEngineOperation("WorkflowStartupCompleted", workflowInstance.Id, 
                    new { Status = executionResult.Status.ToString() });
                
                // PHASE 3: Record workflow suspension metrics
                _workflowMetrics.RecordWorkflowSuspended(
                    workflowSchema.Name ?? "Unknown",
                    workflowDefinitionId.ToString(),
                    "execution_suspended");
            }
            else
            {
                _workflowLogger.LogEngineOperation("WorkflowStartupCompleted", workflowInstance.Id, 
                    new { Status = executionResult.Status.ToString() });
            }

            return executionResult;
        }
        catch (OperationCanceledException)
        {
            // BEFORE: Basic cancellation logging
            // _logger.LogInformation("ORCHESTRATION: Start request canceled for workflow definition {WorkflowDefinitionId}", workflowDefinitionId);
            
            // AFTER: Enhanced cancellation logging with correlation context and metrics
            _workflowLogger.LogEngineOperation("WorkflowStartCancelled", Guid.Empty, 
                new { WorkflowDefinitionId = workflowDefinitionId, CorrelationId = correlationId });
            
            // PHASE 3: Record workflow cancellation metrics
            // BEFORE: No metrics for workflow cancellations
            // AFTER: Track cancellations for monitoring and capacity planning
            _workflowMetrics.RecordWorkflowCancelled(
                "Unknown", // We may not have schema at this point
                workflowDefinitionId.ToString(),
                "start_cancelled",
                new KeyValuePair<string, object?>("correlation_id", correlationId ?? "none"));
                
            throw;
        }
        catch (Exception ex)
        {
            // BEFORE: Basic error logging
            // _logger.LogError(ex, "ORCHESTRATION: Unexpected error while starting workflow {WorkflowDefinitionId}, correlationId {CorrelationId}", workflowDefinitionId, correlationId);
            
            // AFTER: Enhanced error logging with structured context
            _workflowLogger.LogCriticalError($"Unexpected error while starting workflow {workflowDefinitionId}", 
                Guid.Empty, ex, new { WorkflowDefinitionId = workflowDefinitionId, CorrelationId = correlationId });

            // PHASE 3: Record workflow failure metrics
            // BEFORE: No metrics for workflow failures
            // AFTER: Comprehensive failure tracking for alerting and debugging
            _workflowMetrics.RecordWorkflowFailed(
                "Unknown", // We may not have schema at this point
                workflowDefinitionId.ToString(),
                ex.GetType().Name,
                new KeyValuePair<string, object?>("correlation_id", correlationId ?? "none"),
                new KeyValuePair<string, object?>("error_message", ex.Message));

            // Use fault handler for startup failures
            var faultContext = new StartWorkflowFaultContext(
                workflowDefinitionId,
                instanceName,
                startedBy,
                ex,
                1 // First attempt
            );

            try
            {
                var faultResult = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext, cancellationToken);

                if (faultResult.ShouldRetry && faultResult.RetryDelay.HasValue)
                {
                    _logger.LogWarning("ORCHESTRATION: Workflow startup failed, retrying after {Delay}",
                        faultResult.RetryDelay.Value);
                    await Task.Delay(faultResult.RetryDelay.Value, cancellationToken);
                    // Could implement retry logic here, but for now just fail
                }
            }
            catch (Exception faultEx)
            {
                _logger.LogError(faultEx, "ORCHESTRATION: Fault handler failed during workflow startup fault handling");
            }

            if (string.IsNullOrEmpty(correlationId))
                return WorkflowExecutionResult.Failed(null, "Unexpected error occurred during workflow start");

            var workflowInstance =
                await _persistenceService.GetWorkflowInstanceByCorrelationId(correlationId, cancellationToken);

            if (workflowInstance is null)
                return WorkflowExecutionResult.Failed(null, "Unexpected error occurred during workflow start");

            await _lifecycleManager.TransitionWorkflowStateAsync(
                workflowInstance,
                WorkflowStatus.Failed,
                "Workflow failed during startup with unexpected error",
                cancellationToken);

            await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow failed during startup",
                cancellationToken);

            // Write failure event to outbox
            await WriteWorkflowFailedEventAsync(workflowInstance, ex.Message, cancellationToken);

            return WorkflowExecutionResult.Failed(workflowInstance,
                "Unexpected error occurred during workflow start");
        }
            },
            correlationId);
    }

    public async Task<WorkflowExecutionResult> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to enhanced version with null bookmark key
        return await ResumeWorkflowAsync(workflowInstanceId, activityId, completedBy, input, nextAssignmentOverrides,
            null, cancellationToken);
    }

    /// <summary>
    /// Enhanced resume method with bookmark key support for idempotent operations
    /// </summary>
    public async Task<WorkflowExecutionResult> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        string? bookmarkKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ENHANCED: Create correlation scope for resume operation
            using var correlationScope = _workflowLogger.CreateCorrelationScope(workflowInstanceId, null, "ResumeWorkflow");
            
            // BEFORE: Basic resume logging
            // _logger.LogInformation("ORCHESTRATION: Resuming complete workflow for instance {WorkflowInstanceId} at activity {ActivityId}", workflowInstanceId, activityId);
            
            // AFTER: Enhanced resume logging with timing
            var resumeStartTime = DateTime.UtcNow;
            _workflowLogger.LogEngineOperation("WorkflowResumeStarting", workflowInstanceId, 
                new { ActivityId = activityId, CompletedBy = completedBy, BookmarkKey = bookmarkKey });

            // Execute with resilience for the entire resume operation
            return await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
            {
                // 1. Load workflow instance and schema via persistence service
                var workflowInstance =
                    await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, ct);
                if (workflowInstance == null)
                    return WorkflowExecutionResult.Failed(null, $"Workflow instance not found: {workflowInstanceId}");

                var workflowSchema = await _persistenceService.GetWorkflowSchemaAsync(
                    workflowInstance.WorkflowDefinitionId, ct);
                if (workflowSchema == null)
                    return WorkflowExecutionResult.Failed(workflowInstance,
                        $"Workflow definition not found: {workflowInstance.WorkflowDefinitionId}");

                // 2. Consume bookmark if provided (critical for idempotent resume operations)
                if (!string.IsNullOrEmpty(bookmarkKey))
                {
                    var bookmarkResult = await _bookmarkService.ConsumeBookmarkAsync(
                        workflowInstanceId, activityId, bookmarkKey, completedBy, input, ct);

                    if (!bookmarkResult.Success)
                    {
                        var errorMessage = bookmarkResult.FailureReason switch
                        {
                            BookmarkConsumeFailureReason.BookmarkNotFound => "Bookmark not found",
                            BookmarkConsumeFailureReason.BookmarkAlreadyConsumed =>
                                "Bookmark already consumed - operation may be duplicate",
                            BookmarkConsumeFailureReason.WorkflowNotFound => "Workflow not found",
                            BookmarkConsumeFailureReason.WorkflowNotInRunnableState => "Workflow not in runnable state",
                            BookmarkConsumeFailureReason.ActivityNotFound => "Activity not found",
                            BookmarkConsumeFailureReason.ValidationFailed => "Bookmark validation failed",
                            _ => "Unknown bookmark consumption failure"
                        };

                        // For already consumed bookmarks, this might be a duplicate request - handle gracefully
                        if (bookmarkResult.FailureReason == BookmarkConsumeFailureReason.BookmarkAlreadyConsumed)
                        {
                            _logger.LogWarning(
                                "ORCHESTRATION: Bookmark {BookmarkKey} already consumed for workflow {WorkflowId} - potential duplicate resume request",
                                bookmarkKey, workflowInstanceId);
                            // Return current workflow state instead of failing
                            return WorkflowExecutionResult.Pending(workflowInstance, activityId);
                        }

                        return WorkflowExecutionResult.Failed(workflowInstance,
                            $"Bookmark consumption failed: {errorMessage}");
                    }

                    _logger.LogDebug(
                        "ORCHESTRATION: Successfully consumed bookmark {BookmarkKey} for workflow {WorkflowId}",
                        bookmarkKey, workflowInstanceId);
                }

                // 3. Validate current activity and workflow state
                var validationResult = _stateManager.ValidateWorkflowState(
                    workflowInstance,
                    activityId,
                    WorkflowStatus.Suspended);
                if (!validationResult.IsValid)
                {
                    var errorMessage = string.Join(", ", validationResult.ValidationErrors);
                    return WorkflowExecutionResult.Failed(workflowInstance, errorMessage);
                }

                var currentActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == activityId);
                if (currentActivity == null)
                    return WorkflowExecutionResult.Failed(workflowInstance,
                        $"Activity definition not found: {activityId}");

                // 4. Update runtime overrides if provided
                if (nextAssignmentOverrides != null && nextAssignmentOverrides.Any())
                {
                    var updateResult = await _stateManager.UpdateRuntimeOverridesAsync(
                        workflowInstance, nextAssignmentOverrides, ct);

                    if (!updateResult.IsSuccess)
                        return WorkflowExecutionResult.Failed(workflowInstance,
                            updateResult.ErrorMessage ?? "Runtime override update failed");

                    _logger.LogDebug(
                        "ORCHESTRATION: Updated workflow instance {WorkflowInstanceId} with {Count} runtime overrides",
                        workflowInstanceId, nextAssignmentOverrides.Count);
                }

                // 5. Write workflow resume event to outbox
                await WriteWorkflowResumedEventAsync(workflowInstance, activityId, completedBy, ct);

                // ENHANCED: Log workflow resumed with full context
                _workflowLogger.LogWorkflowResumed(workflowInstance, activityId, completedBy);

                // 6. Continue with existing resume logic...
                var executionResult = await ExecuteWorkflowAsync(workflowSchema, workflowInstance,
                    currentActivity,
                    input ?? new Dictionary<string, object>(), true, ct);

                // BEFORE: Basic completion logging
                // _logger.LogInformation("ORCHESTRATION: Complete workflow resume finished with status {Status}", executionResult.Status);
                
                // AFTER: Enhanced completion logging with timing
                var resumeDuration = DateTime.UtcNow - resumeStartTime;
                _workflowLogger.LogPerformanceMetric("WorkflowResumeCompleted", workflowInstanceId, resumeDuration, 
                    new { Status = executionResult.Status.ToString(), ActivityId = activityId });

                return executionResult;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "ORCHESTRATION: Resume request canceled for workflow instance {WorkflowInstanceId} at activity {ActivityId}",
                workflowInstanceId, activityId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ORCHESTRATION: Critical failure in workflow resume for instance {WorkflowInstanceId}",
                workflowInstanceId);

            try
            {
                var workflowInstance =
                    await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
                if (workflowInstance != null)
                {
                    await _lifecycleManager.TransitionWorkflowStateAsync(
                        workflowInstance,
                        WorkflowStatus.Failed,
                        "Workflow failed during resume operation",
                        cancellationToken);

                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow failed during resume",
                        cancellationToken);

                    return WorkflowExecutionResult.Failed(workflowInstance,
                        "Workflow failed during resume operation");
                }
            }
            catch (Exception transitionEx)
            {
                _logger.LogCritical(transitionEx,
                    "ORCHESTRATION: Critical error - failed to transition workflow {WorkflowInstanceId} to failed state during resume error handling",
                    workflowInstanceId);
            }

            return WorkflowExecutionResult.Failed(null, $"Workflow resume failed: {ex.Message}");
        }
    }

    /// <summary>
    /// ENHANCED: Execute workflow following "one step = one transaction" rule
    /// Only executes a single activity step and returns, allowing proper transaction boundaries
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition activityToExecute,
        Dictionary<string, object>? resumeInput = null,
        bool isResume = false,
        CancellationToken cancellationToken = default)
    {
        // PHASE 4: Create workflow execution span for single-step execution
        using var executionSpan = _workflowTracing.CreateWorkflowSpan(
            WorkflowTelemetryConstants.ActivityNames.WorkflowExecution,
            workflowInstance.Id,
            workflowInstance.WorkflowDefinitionId,
            workflowInstance.CorrelationId);
        
        executionSpan.SetWorkflowStatus(workflowInstance.Status.ToString())
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowEngineOperation, isResume ? "resume" : "execute")
            .SetAttribute(WorkflowTelemetryConstants.SemanticAttributes.WorkflowActivityId, activityToExecute.Id);

        try
        {
        _logger.LogInformation("ORCHESTRATION: {Mode} single-step workflow execution for instance {WorkflowInstanceId}",
            isResume ? "Resuming" : "Starting", workflowInstance.Id);

        var context = new WorkflowExecutionContext(workflowSchema, workflowInstance);

        // Write workflow started event for new workflows
        if (!isResume) await WriteWorkflowStartedEventAsync(workflowInstance, cancellationToken);

        // CRITICAL CHANGE: Execute only ONE activity per call (one step = one transaction)
        var currentActivity = activityToExecute;

        // Execute single activity step
        ActivityResult activityResult;
        if (isResume)
        {
            // Resume workflow and execute the current activity with resume input
            await _lifecycleManager.ResumeWorkflowAsync(workflowInstance, "Resuming workflow", cancellationToken);
            activityResult = await ExecuteSingleActivityAsync(context, currentActivity,
                resumeInput ?? new Dictionary<string, object>(), true, cancellationToken);
        }
        else
        {
            // Execute activity normally for new workflow
            activityResult = await ExecuteSingleActivityAsync(context, currentActivity, null, false, cancellationToken);
        }

        // Handle single activity result - return after each step for proper transaction boundaries
        switch (activityResult.Status)
        {
            case ActivityResultStatus.Completed:
            case ActivityResultStatus.Skipped:
                // Checkpoint after activity completion (within same transaction)
                await _stateManager.CreateCheckpointAsync(workflowInstance,
                    $"Activity {currentActivity.Id} completed", cancellationToken);

                // Determine the next activity for the NEXT step
                var nextActivity = await DetermineNextWorkflowActivityAsync(
                    context, currentActivity, activityResult, cancellationToken);

                if (nextActivity == null)
                {
                    // Complete workflow in this step with optimistic concurrency
                    await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
                    {
                        await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, ct);
                        await _stateManager.CreateCheckpointAsync(workflowInstance,
                            "Workflow completed successfully", ct);

                        // Use optimistic concurrency for final workflow update
                        var updateResult = await UpdateWorkflowWithConcurrencyAsync(workflowInstance, ct);
                        if (!updateResult)
                            throw new InvalidOperationException(
                                "Failed to update workflow due to concurrency conflict during completion");

                        return true;
                    }, cancellationToken);

                    return WorkflowExecutionResult.Completed(workflowInstance);
                }
                else
                {
                    // CRITICAL CHANGE: Don't continue to next activity in same transaction
                    // Update workflow pointer for next step with optimistic concurrency
                    await _resilienceService.ExecuteDatabaseOperationAsync(async ct =>
                    {
                        workflowInstance.SetCurrentActivity(nextActivity.Id);
                        await _stateManager.CreateCheckpointAsync(workflowInstance,
                            $"Workflow pointer updated to {nextActivity.Id}", ct);

                        // Use optimistic concurrency for workflow state update
                        var updateResult = await UpdateWorkflowWithConcurrencyAsync(workflowInstance, ct);
                        if (!updateResult)
                            throw new InvalidOperationException(
                                "Failed to update workflow due to concurrency conflict during activity transition");

                        return true;
                    }, cancellationToken);

                    // Return indicating more steps needed - let the caller orchestrate next step
                    return WorkflowExecutionResult.StepCompleted(workflowInstance, nextActivity.Id);
                }

                break;

            case ActivityResultStatus.Failed:
                var errorMessage = activityResult.ErrorMessage ?? "Activity failed";
                _logger.LogWarning("ENGINE: Activity {ActivityId} failed: {Error}",
                    currentActivity.Id, errorMessage);

                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance,
                    WorkflowStatus.Failed,
                    errorMessage,
                    cancellationToken);

                await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow failed", cancellationToken);

                return WorkflowExecutionResult.Failed(workflowInstance, errorMessage);

            case ActivityResultStatus.Pending:
                var pauseReason = $"Activity {currentActivity.Id} requires external completion";
                _logger.LogInformation("ENGINE: Pausing workflow {WorkflowInstanceId}: {Reason}",
                    workflowInstance.Id, pauseReason);

                await _lifecycleManager.PauseWorkflowAsync(workflowInstance, pauseReason, cancellationToken);

                await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow paused", cancellationToken);

                return WorkflowExecutionResult.Pending(workflowInstance, currentActivity.Id);

            default:
                var unknownStatusError = $"Unknown activity result status: {activityResult.Status}";
                _logger.LogError("ENGINE: {Error} for activity {ActivityId}",
                    unknownStatusError, currentActivity.Id);

                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance,
                    WorkflowStatus.Failed,
                    unknownStatusError,
                    cancellationToken);

                await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow failed", cancellationToken);

                return WorkflowExecutionResult.Failed(workflowInstance, unknownStatusError);
        }

        // Should not reach here due to explicit returns in each case
        _logger.LogWarning("ENGINE: Unexpected end of ExecuteWorkflowAsync for {WorkflowInstanceId}",
            workflowInstance.Id);
        return WorkflowExecutionResult.Failed(workflowInstance, "Unexpected workflow execution end");
        }
        catch (Exception ex)
        {
            // PHASE 4: Record exception in tracing span before re-throwing
            executionSpan.RecordException(ex);
            throw;
        }
    }

    [Obsolete("Remain for testing purpose, later will change to new ExecuteSingleActivityAsync")]
    public async Task<ActivityResult> ExecuteActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activity = _activityFactory.CreateActivity(activityDefinition.Type);
            var result = await activity.ExecuteAsync(context, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ENGINE: Failed to execute activity {ActivityId}", activityDefinition.Id);
            return ActivityResult.Failed($"Activity execution failed: {ex.Message}");
        }
    }

    [Obsolete("Remain for testing purpose, later will change to new ExecuteSingleActivityAsync")]
    public async Task<ActivityResult> ResumeActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activity = _activityFactory.CreateActivity(activityDefinition.Type);
            var result = await activity.ResumeAsync(context, resumeInput, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ENGINE: Failed to resume activity {ActivityId}", activityDefinition.Id);
            return ActivityResult.Failed($"Activity resume failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes or resumes a single activity with state management and error handling
    /// ENHANCED: Now includes resilience patterns and fault handling
    /// </summary>
    private async Task<ActivityResult> ExecuteSingleActivityAsync(
        WorkflowExecutionContext context,
        ActivityDefinition activityDefinition,
        Dictionary<string, object>? resumeInput = null,
        bool isResume = false,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // PHASE 4: Create activity execution span for distributed tracing
        return await _workflowTracing.TraceActivityExecutionAsync(
            activityDefinition.Id,
            activityDefinition.Type,
            context.WorkflowInstance.Id,
            Guid.NewGuid(), // Create unique execution ID for this activity execution
            async (activitySpan) =>
            {
                // ENHANCED: Create activity-specific correlation scope for comprehensive tracing
                using var activityScope = _workflowLogger.CreateActivityCorrelationScope(
                    context.WorkflowInstance.Id, activityDefinition.Id, activityDefinition.Type, 
                    context.WorkflowInstance.CorrelationId);

        // BEFORE: Basic activity start logging
        // _logger.LogDebug("ENGINE: Starting {Mode} of activity {ActivityId} of type {ActivityType}", isResume ? "resume" : "execution", activityDefinition.Id, activityDefinition.Type);
        
        // AFTER: Enhanced activity start logging with full context
        _workflowLogger.LogActivityStarting(activityDefinition.Id, activityDefinition.Type, 
            context.WorkflowInstance.Id, isResume);

        try
        {
            // Execute with resilience wrapper for comprehensive fault handling
            return await _resilienceService.ExecuteWorkflowActivityAsync(async ct =>
            {
                try
                {
                    // 1. Create activity context
                    var activityContext = context.CreateActivityContext(activityDefinition);

                    // 2. Execute or resume the activity with resilience protection
                    var activity = _activityFactory.CreateActivity(activityDefinition.Type);

                    ActivityResult activityResult;
                    if (isResume && resumeInput != null)
                        activityResult = await activity.ResumeAsync(activityContext, resumeInput, ct);
                    else
                        activityResult = await activity.ExecuteAsync(activityContext, ct);

                    // 3. Handle activity result with specific processing
                    if (activityResult.Status == ActivityResultStatus.Failed)
                    {
                        // Let fault handler decide how to handle activity failures
                        var faultContext = new ActivityFaultContext(
                            context.WorkflowInstance.Id,
                            activityDefinition.Id,
                            activityDefinition.Type,
                            new Exception(activityResult.ErrorMessage ?? "Activity failed"),
                            1, // First attempt in this execution
                            activityContext.Variables
                        );

                        var faultResult = await _faultHandler.HandleActivityExecutionFaultAsync(faultContext, ct);

                        if (faultResult.ShouldRetry && faultResult.RetryDelay.HasValue)
                        {
                            _logger.LogWarning("ENGINE: Activity {ActivityId} failed, retrying after {Delay}",
                                activityDefinition.Id, faultResult.RetryDelay.Value);
                            await Task.Delay(faultResult.RetryDelay.Value, ct);
                            // The resilience service will handle the retry
                        }

                        return ActivityResult.Failed(activityResult.ErrorMessage ??
                                                     (isResume
                                                         ? "Activity resume failed"
                                                         : "Activity execution failed"));
                    }

                    // 4. Handle Pending status - create bookmark for waiting
                    if (activityResult.Status == ActivityResultStatus.Pending)
                    {
                        await CreateActivityBookmarkAsync(context, activityDefinition, activityResult, ct);
                        // BEFORE: Basic bookmark creation logging
                        // _logger.LogDebug("ENGINE: Created bookmark for pending activity {ActivityId}", activityDefinition.Id);
                        
                        // AFTER: Enhanced pending logging with reason
                        var pendingReason = activityResult.OutputData.TryGetValue("PendingReason", out var reason) 
                            ? reason?.ToString() ?? "Activity requires external completion"
                            : "Activity requires external completion";
                        _workflowLogger.LogActivityPending(activityDefinition.Id, activityDefinition.Type, 
                            context.WorkflowInstance.Id, pendingReason);
                    }

                    // 5. Update workflow variables after activity completes successfully
                    if (activityResult.OutputData.Any())
                    {
                        var stateUpdateResult = await _stateManager.UpdateWorkflowVariablesAsync(
                            context.WorkflowInstance, activityResult.OutputData, ct);

                        if (!stateUpdateResult.IsSuccess)
                            return ActivityResult.Failed(stateUpdateResult.ErrorMessage ?? "Variable update failed");

                        await _stateManager.CreateCheckpointAsync(
                            context.WorkflowInstance,
                            $"Variables updated after {activityDefinition.Id} completion",
                            ct);
                    }

                    // 6. Write outbox events for activity completion
                    if (activityResult.Status == ActivityResultStatus.Completed)
                        await WriteActivityCompletedEventAsync(context, activityDefinition, activityResult, ct);

                    // 7. Track execution in context metadata
                    var duration = DateTime.UtcNow - startTime;
                    context.TrackExecutionStep(activityDefinition.Id, activityResult.Status, duration);

                    // BEFORE: Basic activity completion logging
                    // _logger.LogDebug("ENGINE: Successfully completed execution of activity {ActivityId} in {Duration}ms with status {Status}", activityDefinition.Id, duration.TotalMilliseconds, activityResult.Status);
                    
                    // AFTER: Enhanced activity completion logging with performance metrics
                    _workflowLogger.LogActivityCompleted(activityDefinition.Id, activityDefinition.Type, 
                        context.WorkflowInstance.Id, duration, activityResult.Status);

                    return activityResult;
                }
                catch (Exception activityEx)
                {
                    // Handle activity-specific exceptions with fault handler
                    var faultContext = new ActivityFaultContext(
                        context.WorkflowInstance.Id,
                        activityDefinition.Id,
                        activityDefinition.Type,
                        activityEx,
                        1,
                        context.WorkflowInstance.Variables
                    );

                    var faultResult = await _faultHandler.HandleActivityExecutionFaultAsync(faultContext, ct);

                    if (faultResult.SuspendWorkflow)
                    {
                        _logger.LogError(
                            "ENGINE: Suspending workflow {WorkflowId} due to critical activity failure in {ActivityId}",
                            context.WorkflowInstance.Id, activityDefinition.Id);

                        await _lifecycleManager.TransitionWorkflowStateAsync(
                            context.WorkflowInstance,
                            WorkflowStatus.Suspended,
                            $"Activity {activityDefinition.Id} failed critically: {activityEx.Message}",
                            ct);
                    }

                    // Re-throw to let resilience service handle retries
                    throw;
                }
            }, activityDefinition.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            context.TrackExecutionStep(activityDefinition.Id, ActivityResultStatus.Failed, duration);

            // BEFORE: Basic error logging
            // _logger.LogError(ex, "ENGINE: Critical error during execution of activity {ActivityId} after resilience handling", activityDefinition.Id);
            
            // AFTER: Enhanced error logging with full context and timing
            _workflowLogger.LogActivityFailed(activityDefinition.Id, activityDefinition.Type, 
                context.WorkflowInstance.Id, "Critical error during execution after resilience handling", ex);

            return ActivityResult.Failed($"Activity execution failed: {ex.Message}");
        }
            });
    }

    /// <summary>
    /// Determines the next activity to execute after a completed activity
    /// </summary>
    private async Task<ActivityDefinition?> DetermineNextWorkflowActivityAsync(
        WorkflowExecutionContext context,
        ActivityDefinition currentActivity,
        ActivityResult activityResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("ENGINE: Determining next activity after {ActivityId}", currentActivity.Id);

            // Determine next activity using flow control manager
            var nextActivityId = await _flowControlManager.DetermineNextActivityAsync(
                context.Schema, currentActivity.Id, activityResult,
                context.WorkflowInstance.Variables, cancellationToken);

            if (string.IsNullOrEmpty(nextActivityId))
            {
                _logger.LogDebug("ENGINE: No next activity found - workflow will complete");
                return null; // Indicates workflow completion
            }

            var nextActivity = context.Schema.Activities.FirstOrDefault(a => a.Id == nextActivityId);
            if (nextActivity == null)
            {
                var errorMessage = $"Unknown workflow activity: {nextActivityId}";
                _logger.LogError("ENGINE: {Error}", errorMessage);

                throw new InvalidOperationException(errorMessage);
            }

            // Advance workflow to the next activity
            await _lifecycleManager.AdvanceWorkflowAsync(context.WorkflowInstance, nextActivityId,
                cancellationToken: cancellationToken);

            _logger.LogDebug("ENGINE: Successfully determined next activity {NextActivityId}", nextActivityId);
            return nextActivity;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var errorMessage = $"Error determining next activity after {currentActivity.Id}: {ex.Message}";
            _logger.LogError(ex, "ENGINE: {Error}", errorMessage);

            throw;
        }
    }

    public async Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("ORCHESTRATION: Starting workflow definition validation");

            // Basic validation
            if (string.IsNullOrEmpty(workflowSchema.Name))
            {
                _logger.LogWarning("Workflow schema validation failed: Name is required");
                return false;
            }

            if (!workflowSchema.Activities.Any())
            {
                _logger.LogWarning("Workflow schema validation failed: At least one activity is required");
                return false;
            }

            // Validate transitions using a flow control manager
            if (!_flowControlManager.ValidateWorkflowTransitions(workflowSchema))
            {
                _logger.LogWarning("Workflow schema validation failed: Invalid workflow transitions");
                return false;
            }

            // Validate each activity using a coordinated approach
            var dummyWorkflowInstance = WorkflowInstance.Create(
                Guid.NewGuid(),
                "ValidationInstance",
                null,
                "System",
                workflowSchema.Variables);

            var context = new WorkflowExecutionContext(workflowSchema, dummyWorkflowInstance);

            foreach (var activity in workflowSchema.Activities)
            {
                var workflowActivity = _activityFactory.CreateActivity(activity.Type);
                var activityContext = context.CreateActivityContext(activity);

                var validationResult = await workflowActivity.ValidateAsync(activityContext, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Activity {ActivityId} validation failed: {Errors}",
                        activity.Id, string.Join(", ", validationResult.Errors));
                    return false;
                }
            }

            _logger.LogDebug("ORCHESTRATION: Workflow definition validation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Error during workflow definition validation");
            return false;
        }
    }

    /// <summary>
    /// Creates appropriate bookmark for pending activity
    /// </summary>
    private async Task CreateActivityBookmarkAsync(
        WorkflowExecutionContext context,
        ActivityDefinition activityDefinition,
        ActivityResult activityResult,
        CancellationToken cancellationToken)
    {
        var bookmarkKey = $"{activityDefinition.Id}_{Guid.NewGuid():N}";

        // Determine bookmark type based on activity type
        WorkflowBookmark bookmark;

        // Default to user action bookmark - can be enhanced based on activity type
        if (activityDefinition.Type.Contains("Timer", StringComparison.OrdinalIgnoreCase))
        {
            // Timer-based activity - create timer bookmark
            var dueTime = activityResult.OutputData.TryGetValue("DueAt", out var dueAtValue) &&
                          DateTime.TryParse(dueAtValue?.ToString(), out var dueAt)
                ? dueAt
                : DateTime.UtcNow.AddMinutes(30); // Default 30 minutes

            bookmark = await _bookmarkService.CreateTimerBookmarkAsync(
                context.WorkflowInstance.Id,
                activityDefinition.Id,
                bookmarkKey,
                dueTime,
                context.WorkflowInstance.CorrelationId,
                JsonSerializer.Serialize(activityResult.OutputData),
                cancellationToken);
        }
        else if (activityDefinition.Type.Contains("External", StringComparison.OrdinalIgnoreCase) ||
                 activityDefinition.Type.Contains("Webhook", StringComparison.OrdinalIgnoreCase))
        {
            // External message bookmark
            bookmark = await _bookmarkService.CreateExternalMessageBookmarkAsync(
                context.WorkflowInstance.Id,
                activityDefinition.Id,
                bookmarkKey,
                context.WorkflowInstance.CorrelationId,
                JsonSerializer.Serialize(activityResult.OutputData),
                cancellationToken);
        }
        else
        {
            // Default to user action bookmark
            bookmark = await _bookmarkService.CreateUserActionBookmarkAsync(
                context.WorkflowInstance.Id,
                activityDefinition.Id,
                bookmarkKey,
                context.WorkflowInstance.CorrelationId,
                JsonSerializer.Serialize(activityResult.OutputData),
                cancellationToken);
        }

        _logger.LogDebug("ENGINE: Created {BookmarkType} bookmark {BookmarkId} for activity {ActivityId}",
            bookmark.Type, bookmark.Id, activityDefinition.Id);
    }

    /// <summary>
    /// Writes activity completed event to outbox for reliable publishing
    /// </summary>
    private async Task WriteActivityCompletedEventAsync(
        WorkflowExecutionContext context,
        ActivityDefinition activityDefinition,
        ActivityResult activityResult,
        CancellationToken cancellationToken)
    {
        var eventData = new
        {
            WorkflowInstanceId = context.WorkflowInstance.Id,
            ActivityId = activityDefinition.Id,
            ActivityType = activityDefinition.Type,
            CompletedAt = DateTime.UtcNow,
            OutputData = activityResult.OutputData,
            CorrelationId = context.WorkflowInstance.CorrelationId
        };

        var outboxEvent = WorkflowOutbox.Create(
            "ActivityCompleted",
            JsonSerializer.Serialize(eventData),
            new Dictionary<string, string>
            {
                ["WorkflowInstanceId"] = context.WorkflowInstance.Id.ToString(),
                ["ActivityId"] = activityDefinition.Id,
                ["ActivityType"] = activityDefinition.Type,
                ["CorrelationId"] = context.WorkflowInstance.CorrelationId ?? ""
            }
        );

        await _outboxRepository.AddAsync(outboxEvent, cancellationToken);

        _logger.LogDebug("ENGINE: Queued ActivityCompleted event for {ActivityId} in workflow {WorkflowId}",
            activityDefinition.Id, context.WorkflowInstance.Id);
    }

    /// <summary>
    /// Writes workflow resumed event to outbox for reliable publishing
    /// </summary>
    private async Task WriteWorkflowResumedEventAsync(
        WorkflowInstance workflowInstance,
        string activityId,
        string completedBy,
        CancellationToken cancellationToken)
    {
        var eventData = new
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = activityId,
            CompletedBy = completedBy,
            ResumedAt = DateTime.UtcNow,
            CorrelationId = workflowInstance.CorrelationId
        };

        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowResumed",
            JsonSerializer.Serialize(eventData),
            new Dictionary<string, string>
            {
                ["WorkflowInstanceId"] = workflowInstance.Id.ToString(),
                ["ActivityId"] = activityId,
                ["CompletedBy"] = completedBy,
                ["CorrelationId"] = workflowInstance.CorrelationId ?? ""
            }
        );

        await _outboxRepository.AddAsync(outboxEvent, cancellationToken);

        _logger.LogDebug("ENGINE: Queued WorkflowResumed event for workflow {WorkflowId} at activity {ActivityId}",
            workflowInstance.Id, activityId);
    }

    /// <summary>
    /// Writes workflow failed event to outbox for reliable publishing
    /// </summary>
    private async Task WriteWorkflowFailedEventAsync(
        WorkflowInstance workflowInstance,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var eventData = new
        {
            WorkflowInstanceId = workflowInstance.Id,
            ErrorMessage = errorMessage,
            FailedAt = DateTime.UtcNow,
            CorrelationId = workflowInstance.CorrelationId,
            CurrentActivityId = workflowInstance.CurrentActivityId
        };

        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowFailed",
            JsonSerializer.Serialize(eventData),
            new Dictionary<string, string>
            {
                ["WorkflowInstanceId"] = workflowInstance.Id.ToString(),
                ["CorrelationId"] = workflowInstance.CorrelationId ?? "",
                ["CurrentActivityId"] = workflowInstance.CurrentActivityId ?? ""
            }
        );

        await _outboxRepository.AddAsync(outboxEvent, cancellationToken);

        _logger.LogDebug("ENGINE: Queued WorkflowFailed event for workflow {WorkflowId}",
            workflowInstance.Id);
    }

    /// <summary>
    /// Writes workflow started event to outbox for reliable publishing
    /// </summary>
    private async Task WriteWorkflowStartedEventAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken)
    {
        var eventData = new
        {
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowDefinitionId = workflowInstance.WorkflowDefinitionId,
            InstanceName = workflowInstance.Name,
            StartedBy = workflowInstance.StartedBy,
            StartedAt = workflowInstance.CreatedOn,
            CorrelationId = workflowInstance.CorrelationId
        };

        var outboxEvent = WorkflowOutbox.Create(
            "WorkflowStarted",
            JsonSerializer.Serialize(eventData),
            new Dictionary<string, string>
            {
                ["WorkflowInstanceId"] = workflowInstance.Id.ToString(),
                ["WorkflowDefinitionId"] = workflowInstance.WorkflowDefinitionId.ToString(),
                ["StartedBy"] = workflowInstance.StartedBy ?? "",
                ["CorrelationId"] = workflowInstance.CorrelationId ?? ""
            }
        );

        // PHASE 4: Trace database operation for outbox event persistence
        using var dbSpan = _workflowTracing.CreateDatabaseSpan(
            "save",
            "WorkflowOutbox",
            workflowInstance.Id);
        
        dbSpan.SetAttribute("event_type", "WorkflowStarted")
            .SetAttribute("db.operation_name", "AddAsync");

        try
        {
            await _outboxRepository.AddAsync(outboxEvent, cancellationToken);
            dbSpan.Complete();
        }
        catch (Exception ex)
        {
            dbSpan.RecordException(ex);
            throw;
        }

        _logger.LogDebug("ENGINE: Queued WorkflowStarted event for workflow {WorkflowId}",
            workflowInstance.Id);
    }

    /// <summary>
    /// Updates workflow instance using optimistic concurrency control with retry logic
    /// </summary>
    private async Task<bool> UpdateWorkflowWithConcurrencyAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        var attemptCount = 0;

        while (attemptCount < maxRetries)
            try
            {
                var success = await _workflowInstanceRepository
                    .TryUpdateWithConcurrencyAsync(workflowInstance, cancellationToken);

                if (success)
                {
                    _logger.LogDebug("ENGINE: Successfully updated workflow {WorkflowId} with concurrency control",
                        workflowInstance.Id);
                    return true;
                }

                attemptCount++;

                if (attemptCount < maxRetries)
                {
                    _logger.LogWarning(
                        "ENGINE: Concurrency conflict updating workflow {WorkflowId}, attempt {Attempt}/{MaxRetries}",
                        workflowInstance.Id, attemptCount, maxRetries);

                    // Reload the latest version of the workflow
                    var latestInstance =
                        await _persistenceService.GetWorkflowInstanceAsync(workflowInstance.Id, cancellationToken);
                    if (latestInstance == null)
                    {
                        _logger.LogError("ENGINE: Workflow {WorkflowId} not found during concurrency retry",
                            workflowInstance.Id);
                        return false;
                    }

                    // Apply the current changes to the latest version
                    // This is a simplified merge - in production you'd want more sophisticated conflict resolution
                    // Copy critical state from the current version to the latest version
                    // This is a simplified merge strategy - more sophisticated conflict resolution may be needed
                    if (!string.IsNullOrEmpty(workflowInstance.CurrentActivityId))
                        latestInstance.SetCurrentActivity(workflowInstance.CurrentActivityId);

                    if (workflowInstance.Status != WorkflowStatus.Running) // Only update if status changed
                        latestInstance.UpdateStatus(workflowInstance.Status);

                    if (!string.IsNullOrEmpty(workflowInstance.ErrorMessage))
                        latestInstance.UpdateStatus(latestInstance.Status, workflowInstance.ErrorMessage);

                    workflowInstance = latestInstance;

                    // Brief delay before retry
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attemptCount), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ENGINE: Error updating workflow {WorkflowId} with concurrency control, attempt {Attempt}",
                    workflowInstance.Id, attemptCount + 1);
                return false;
            }

        _logger.LogError("ENGINE: Failed to update workflow {WorkflowId} after {MaxRetries} concurrency retry attempts",
            workflowInstance.Id, maxRetries);
        return false;
    }
}