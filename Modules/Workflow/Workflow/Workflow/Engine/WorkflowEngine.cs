using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;

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
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowActivityFactory activityFactory,
        IFlowControlManager flowControlManager,
        IWorkflowLifecycleManager lifecycleManager,
        IWorkflowPersistenceService persistenceService,
        IWorkflowStateManager stateManager,
        ILogger<WorkflowEngine> logger)
    {
        _activityFactory = activityFactory;
        _flowControlManager = flowControlManager;
        _lifecycleManager = lifecycleManager;
        _persistenceService = persistenceService;
        _stateManager = stateManager;
        _logger = logger;
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
        try
        {
            _logger.LogInformation(
                "ORCHESTRATION: Starting workflow for definition {WorkflowDefinitionId}, correlationId {CorrelationId}",
                workflowDefinitionId, correlationId ?? "N/A");

            // 1. Load workflow schema via persistence service
            var workflowSchema =
                await _persistenceService.GetWorkflowSchemaAsync(workflowDefinitionId, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(null, $"Workflow definition not found: {workflowDefinitionId}");

            // 2. Initialize a workflow instance via lifecycle manager
            var workflowInstance = _lifecycleManager.InitializeWorkflowAsync(
                workflowDefinitionId, workflowSchema, instanceName, startedBy, initialVariables, correlationId,
                assignmentOverrides, cancellationToken);

            // 3. Get start activity or 1st activity if none is specified
            var startActivity = _flowControlManager.GetStartActivity(workflowSchema);

            // 4. Execute the workflow from the start activity
            var executionResult =
                await ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity, null, false,
                    cancellationToken);

            _logger.LogInformation("ORCHESTRATION: Workflow startup finished with status {Status}",
                executionResult.Status);

            return executionResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "ORCHESTRATION: Start request canceled for workflow definition {WorkflowDefinitionId}",
                workflowDefinitionId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ORCHESTRATION: Unexpected error while starting workflow {WorkflowDefinitionId}, correlationId {CorrelationId}",
                workflowDefinitionId, correlationId);

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

            return WorkflowExecutionResult.Failed(workflowInstance,
                "Unexpected error occurred during workflow start");
        }
    }

    public async Task<WorkflowExecutionResult> ResumeWorkflowAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object>? input = null,
        Dictionary<string, RuntimeOverride>? nextAssignmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "ORCHESTRATION: Resuming complete workflow for instance {WorkflowInstanceId} at activity {ActivityId}",
                workflowInstanceId, activityId);

            // 1. Load workflow instance and schema via persistence service
            var workflowInstance =
                await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
            if (workflowInstance == null)
                return WorkflowExecutionResult.Failed(null, $"Workflow instance not found: {workflowInstanceId}");

            var workflowSchema = await _persistenceService.GetWorkflowSchemaAsync(
                workflowInstance.WorkflowDefinitionId, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Workflow definition not found: {workflowInstance.WorkflowDefinitionId}");

            // 2. Validate current activity and workflow state
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

            // 3. Update runtime overrides if provided
            if (nextAssignmentOverrides != null && nextAssignmentOverrides.Any())
            {
                var updateResult = await _stateManager.UpdateRuntimeOverridesAsync(
                    workflowInstance, nextAssignmentOverrides, cancellationToken);

                if (!updateResult.IsSuccess)
                    return WorkflowExecutionResult.Failed(workflowInstance,
                        updateResult.ErrorMessage ?? "Runtime override update failed");

                _logger.LogDebug(
                    "ORCHESTRATION: Updated workflow instance {WorkflowInstanceId} with {Count} runtime overrides",
                    workflowInstanceId, nextAssignmentOverrides.Count);
            }

            // 4. Resume the workflow execution using enhanced ExecuteWorkflowAsync
            var executionResult = await ExecuteWorkflowAsync(workflowSchema, workflowInstance,
                currentActivity,
                input ?? new Dictionary<string, object>(), true, cancellationToken);

            _logger.LogInformation("ORCHESTRATION: Complete workflow resume finished with status {Status}",
                executionResult.Status);

            return executionResult;
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

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition activityToExecute,
        Dictionary<string, object>? resumeInput = null,
        bool isResume = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ORCHESTRATION: {Mode} workflow execution for instance {WorkflowInstanceId}",
            isResume ? "Resuming" : "Starting", workflowInstance.Id);

        var context = new WorkflowExecutionContext(workflowSchema, workflowInstance);

        var activitiesToExecute = new Queue<ActivityDefinition>();
        activitiesToExecute.Enqueue(activityToExecute);

        // Track if this is the first activity to handle resume vs. start logic
        var isFirstActivity = true;

        while (activitiesToExecute.Count > 0)
        {
            var currentActivity = activitiesToExecute.Dequeue();

            // Handle the first activity differently for start vs. resume
            ActivityResult activityResult;
            if (isFirstActivity && isResume)
            {
                // Resume workflow and execute the current activity with resume input
                await _lifecycleManager.ResumeWorkflowAsync(workflowInstance, "Resuming workflow", cancellationToken);
                activityResult = await ExecuteSingleActivityAsync(context, currentActivity,
                    resumeInput ?? new Dictionary<string, object>(), true, cancellationToken);
            }
            else
            {
                // Execute activity normally for new workflow or subsequent activities
                activityResult =
                    await ExecuteSingleActivityAsync(context, currentActivity, null, false, cancellationToken);
            }

            isFirstActivity = false; // Only the first activity gets special resume treatment

            // Handle activity results based on status
            switch (activityResult.Status)
            {
                case ActivityResultStatus.Completed:
                case ActivityResultStatus.Skipped: // Treat skipped the same as completed
                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Activity completed",
                        cancellationToken);

                    // Determine the next activity
                    var nextActivity = await DetermineNextWorkflowActivityAsync(
                        context, currentActivity, activityResult, cancellationToken);

                    if (nextActivity == null)
                    {
                        await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);

                        // Checkpoint successful workflow completion
                        await _stateManager.CreateCheckpointAsync(workflowInstance,
                            "Workflow completed successfully", cancellationToken);

                        return WorkflowExecutionResult.Completed(workflowInstance);
                    }

                    // Continue to next activity
                    activitiesToExecute.Enqueue(nextActivity);

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
        }

        // This should not be reached due to the logic above, but keeping as a safety net
        _logger.LogInformation("ENGINE: Completing workflow {WorkflowInstanceId} (fallback)",
            workflowInstance.Id);

        await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);

        // Checkpoint fallback workflow completion
        await _stateManager.CreateCheckpointAsync(workflowInstance,
            "Workflow completed via fallback path", cancellationToken);

        return WorkflowExecutionResult.Completed(workflowInstance);
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
    /// </summary>
    private async Task<ActivityResult> ExecuteSingleActivityAsync(
        WorkflowExecutionContext context,
        ActivityDefinition activityDefinition,
        Dictionary<string, object>? resumeInput = null,
        bool isResume = false,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogDebug("ENGINE: Starting {Mode} of activity {ActivityId} of type {ActivityType}",
            isResume ? "resume" : "execution", activityDefinition.Id, activityDefinition.Type);

        try
        {
            // 1. Create activity context
            var activityContext = context.CreateActivityContext(activityDefinition);

            // 2. Execute or resume the activity (no engine-wide transaction)
            var activity = _activityFactory.CreateActivity(activityDefinition.Type);

            ActivityResult activityResult;
            if (isResume && resumeInput != null)
                activityResult = await activity.ResumeAsync(activityContext, resumeInput, cancellationToken);
            else
                activityResult = await activity.ExecuteAsync(activityContext, cancellationToken);

            if (activityResult.Status == ActivityResultStatus.Failed)
                return ActivityResult.Failed(activityResult.ErrorMessage ??
                                             (isResume ? "Activity resume failed" : "Activity execution failed"));

            // 3. Update workflow variables after activity completes
            if (activityResult.OutputData.Any())
            {
                // Update variables in memory (no database operation)
                var stateUpdateResult = await _stateManager.UpdateWorkflowVariablesAsync(
                    context.WorkflowInstance, activityResult.OutputData, cancellationToken);

                if (!stateUpdateResult.IsSuccess)
                    return ActivityResult.Failed(stateUpdateResult.ErrorMessage ?? "Variable update failed");

                await _stateManager.CreateCheckpointAsync(
                    context.WorkflowInstance,
                    $"Variables updated after {activityDefinition.Id} completion",
                    cancellationToken);
            }

            // 4. Track execution in context metadata
            var duration = DateTime.UtcNow - startTime;
            context.TrackExecutionStep(activityDefinition.Id, activityResult.Status, duration);

            _logger.LogDebug(
                "ENGINE: Successfully completed execution of activity {ActivityId} in {Duration}ms",
                activityDefinition.Id, duration.TotalMilliseconds);

            return activityResult;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            context.TrackExecutionStep(activityDefinition.Id, ActivityResultStatus.Failed, duration);

            _logger.LogError(ex, "ENGINE: Critical error during execution of activity {ActivityId}",
                activityDefinition.Id);

            return ActivityResult.Failed($"Activity execution failed: {ex.Message}");
        }
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
}