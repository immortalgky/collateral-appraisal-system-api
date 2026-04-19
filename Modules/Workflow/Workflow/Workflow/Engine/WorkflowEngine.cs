using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
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
    private readonly IWorkflowDefinitionVersionRepository _versionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowActivityFactory activityFactory,
        IFlowControlManager flowControlManager,
        IWorkflowLifecycleManager lifecycleManager,
        IWorkflowPersistenceService persistenceService,
        IWorkflowStateManager stateManager,
        IWorkflowDefinitionVersionRepository versionRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<WorkflowEngine> logger)
    {
        _activityFactory = activityFactory;
        _flowControlManager = flowControlManager;
        _lifecycleManager = lifecycleManager;
        _persistenceService = persistenceService;
        _stateManager = stateManager;
        _versionRepository = versionRepository;
        _dateTimeProvider = dateTimeProvider;
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

            // 1. Resolve the currently Published version for this definition (pin the instance to it)
            var publishedVersion =
                await _versionRepository.GetCurrentPublishedAsync(workflowDefinitionId, cancellationToken);
            if (publishedVersion == null)
                return WorkflowExecutionResult.Failed(null,
                    $"No Published workflow version exists for definition {workflowDefinitionId}");
            if (publishedVersion.Status == VersionStatus.Deprecated)
                return WorkflowExecutionResult.Failed(null,
                    $"Workflow definition {workflowDefinitionId} has only a Deprecated version; cannot start new instances");

            // 2. Load workflow schema from the resolved version (never by definition id)
            var workflowSchema =
                await _persistenceService.GetSchemaByVersionIdAsync(publishedVersion.Id, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(null, $"Workflow definition not found: {workflowDefinitionId}");

            // 3. Initialize a workflow instance via lifecycle manager, pinning it to this version
            var workflowInstance = await _lifecycleManager.InitializeWorkflowAsync(
                workflowDefinitionId, publishedVersion.Id, workflowSchema, instanceName, startedBy, initialVariables,
                correlationId,
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

            var workflowSchema = await _persistenceService.GetSchemaByVersionIdAsync(
                workflowInstance.WorkflowDefinitionVersionId, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Workflow version not found: {workflowInstance.WorkflowDefinitionVersionId}");

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

            // 4. Ensure completedBy is available in resume input for activities
            var resumeInput = input ?? new Dictionary<string, object>();
            if (!resumeInput.ContainsKey("completedBy") && !string.IsNullOrEmpty(completedBy))
                resumeInput["completedBy"] = completedBy;

            // Resume the workflow execution using enhanced ExecuteWorkflowAsync
            var executionResult = await ExecuteWorkflowAsync(workflowSchema, workflowInstance,
                currentActivity,
                resumeInput, true, cancellationToken);

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
                case ActivityResultStatus.Skipped:
                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Activity completed",
                        cancellationToken);

                    // --- Fork handling: if a ForkActivity just completed, start parallel branches ---
                    if (currentActivity.Type == ActivityTypes.ForkActivity)
                    {
                        var forkResult = await HandleForkCompletionAsync(
                            context, currentActivity, activityResult, cancellationToken);
                        if (forkResult != null)
                            return forkResult;
                        // If forkResult is null, no branches were created (e.g., 0 active branches) - fall through to normal next
                    }

                    // --- Branch completion: if in parallel mode and a branch activity completed ---
                    if (workflowInstance.IsInParallelMode() && isResume)
                    {
                        var branchResult = await HandleBranchActivityCompletionAsync(
                            context, currentActivity, activityResult, cancellationToken);
                        if (branchResult != null)
                            return branchResult;
                    }

                    // Determine the next activity (normal single-path flow)
                    var nextActivity = await DetermineNextWorkflowActivityAsync(
                        context, currentActivity, activityResult, cancellationToken);

                    if (nextActivity == null)
                    {
                        await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);
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
        var startTime = _dateTimeProvider.ApplicationNow;

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
            var duration = _dateTimeProvider.ApplicationNow - startTime;
            context.TrackExecutionStep(activityDefinition.Id, activityResult.Status, duration);

            _logger.LogDebug(
                "ENGINE: Successfully completed execution of activity {ActivityId} in {Duration}ms",
                activityDefinition.Id, duration.TotalMilliseconds);

            return activityResult;
        }
        catch (Exception ex)
        {
            var duration = _dateTimeProvider.ApplicationNow - startTime;
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

    /// <summary>
    /// Handles fork activity completion: creates branch activity states and starts first branch.
    /// Returns a WorkflowExecutionResult if the engine should stop (e.g., pending), or null to continue normal flow.
    /// </summary>
    private async Task<WorkflowExecutionResult?> HandleForkCompletionAsync(
        WorkflowExecutionContext context,
        ActivityDefinition forkActivity,
        ActivityResult activityResult,
        CancellationToken cancellationToken)
    {
        var workflowInstance = context.WorkflowInstance;
        var forkId = activityResult.OutputData.TryGetValue("forkId", out var fid) ? fid?.ToString() : null;

        if (string.IsNullOrEmpty(forkId))
            return null; // No fork context, fall through to normal routing

        var branchTargets = _flowControlManager.DetermineNextActivitiesForFork(
            context.Schema, forkActivity.Id, activityResult);

        if (!branchTargets.Any())
        {
            _logger.LogDebug("ENGINE: Fork {ForkId} produced no branch targets, continuing normally", forkId);
            return null;
        }

        _logger.LogInformation("ENGINE: Fork {ForkId} creating {Count} branch activities",
            forkId, branchTargets.Count);

        // Create BranchActivityState entries for all branches
        foreach (var (branchId, activityId) in branchTargets)
        {
            workflowInstance.AddBranchActivity(new BranchActivityState
            {
                ForkId = forkId,
                BranchId = branchId,
                ActivityId = activityId,
                Status = "Pending"
            });
        }

        // Execute the first branch activity
        var firstBranch = branchTargets.First();
        var firstBranchActivityDef = context.Schema.Activities.FirstOrDefault(a => a.Id == firstBranch.ActivityId);

        if (firstBranchActivityDef == null)
        {
            return WorkflowExecutionResult.Failed(workflowInstance,
                $"Branch activity not found: {firstBranch.ActivityId}");
        }

        // Set current activity to first branch
        await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, firstBranch.ActivityId,
            cancellationToken: cancellationToken);

        // Execute it
        var branchResult = await ExecuteSingleActivityAsync(context, firstBranchActivityDef, null, false, cancellationToken);

        switch (branchResult.Status)
        {
            case ActivityResultStatus.Pending:
                var pauseReason = $"Branch activity {firstBranch.ActivityId} requires external completion (fork {forkId})";
                await _lifecycleManager.PauseWorkflowAsync(workflowInstance, pauseReason, cancellationToken);
                await _stateManager.CreateCheckpointAsync(workflowInstance, "Fork branch paused", cancellationToken);
                return WorkflowExecutionResult.Pending(workflowInstance, firstBranch.ActivityId);

            case ActivityResultStatus.Failed:
                return WorkflowExecutionResult.Failed(workflowInstance,
                    branchResult.ErrorMessage ?? "Branch activity failed");

            case ActivityResultStatus.Completed:
            case ActivityResultStatus.Skipped:
                // Branch activity completed immediately (e.g., auto-activity).
                // Mark this branch as completed and advance it.
                await _stateManager.CreateCheckpointAsync(workflowInstance, "Branch activity completed immediately", cancellationToken);
                // Delegate to branch completion handler
                var completionResult = await HandleBranchActivityCompletionAsync(
                    context, firstBranchActivityDef, branchResult, cancellationToken);
                return completionResult;

            default:
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Unknown branch activity result: {branchResult.Status}");
        }
    }

    /// <summary>
    /// After a branch activity completes, advance the branch or evaluate join.
    /// Returns a WorkflowExecutionResult, or null if the engine should continue normal flow.
    /// </summary>
    private async Task<WorkflowExecutionResult?> HandleBranchActivityCompletionAsync(
        WorkflowExecutionContext context,
        ActivityDefinition completedActivity,
        ActivityResult activityResult,
        CancellationToken cancellationToken)
    {
        var workflowInstance = context.WorkflowInstance;
        var branchState = workflowInstance.GetBranchActivity(completedActivity.Id);

        if (branchState == null)
            return null; // Not a branch activity, fall through to normal flow

        var forkId = branchState.ForkId;
        var branchId = branchState.BranchId;

        _logger.LogDebug("ENGINE: Branch activity {ActivityId} completed for fork {ForkId} branch {BranchId}",
            completedActivity.Id, forkId, branchId);

        // Determine next activity in this branch
        var nextActivityId = await _flowControlManager.DetermineNextActivityAsync(
            context.Schema, completedActivity.Id, activityResult,
            workflowInstance.Variables, cancellationToken);

        if (string.IsNullOrEmpty(nextActivityId))
        {
            // Branch has no next activity - mark completed and remove
            workflowInstance.RemoveBranchActivity(forkId, branchId);
            return await PickNextBranchOrComplete(context, forkId, cancellationToken);
        }

        var nextActivityDef = context.Schema.Activities.FirstOrDefault(a => a.Id == nextActivityId);
        if (nextActivityDef == null)
            return WorkflowExecutionResult.Failed(workflowInstance, $"Unknown activity: {nextActivityId}");

        // Is the next activity a JoinActivity?
        if (nextActivityDef.Type == ActivityTypes.JoinActivity)
        {
            // Record branch result in fork results variable
            RecordBranchResult(workflowInstance, forkId, branchId, activityResult);
            workflowInstance.RemoveBranchActivity(forkId, branchId);

            // Try executing the join to see if condition is met
            await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, nextActivityId,
                cancellationToken: cancellationToken);
            var joinResult = await ExecuteSingleActivityAsync(context, nextActivityDef, null, false, cancellationToken);

            if (joinResult.Status == ActivityResultStatus.Completed)
            {
                // Join condition met - clear all branch state for this fork and continue past join
                workflowInstance.ClearBranchActivities(forkId);
                _logger.LogInformation("ENGINE: Join completed for fork {ForkId}, continuing workflow", forkId);

                // Determine next activity after join
                var afterJoinId = await _flowControlManager.DetermineNextActivityAsync(
                    context.Schema, nextActivityDef.Id, joinResult,
                    workflowInstance.Variables, cancellationToken);

                if (string.IsNullOrEmpty(afterJoinId))
                {
                    await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);
                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Workflow completed after join", cancellationToken);
                    return WorkflowExecutionResult.Completed(workflowInstance);
                }

                var afterJoinDef = context.Schema.Activities.FirstOrDefault(a => a.Id == afterJoinId);
                if (afterJoinDef == null)
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Unknown activity: {afterJoinId}");

                await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, afterJoinId,
                    cancellationToken: cancellationToken);

                // Execute the post-join activity
                var postJoinResult = await ExecuteSingleActivityAsync(context, afterJoinDef, null, false, cancellationToken);
                switch (postJoinResult.Status)
                {
                    case ActivityResultStatus.Pending:
                        await _lifecycleManager.PauseWorkflowAsync(workflowInstance,
                            $"Activity {afterJoinId} requires external completion", cancellationToken);
                        await _stateManager.CreateCheckpointAsync(workflowInstance, "Paused after join", cancellationToken);
                        return WorkflowExecutionResult.Pending(workflowInstance, afterJoinId);
                    case ActivityResultStatus.Failed:
                        return WorkflowExecutionResult.Failed(workflowInstance, postJoinResult.ErrorMessage ?? "Post-join activity failed");
                    case ActivityResultStatus.Completed:
                    case ActivityResultStatus.Skipped:
                        // Continue normal execution - return null to let the main loop handle it
                        return null;
                    default:
                        return WorkflowExecutionResult.Failed(workflowInstance, $"Unknown status: {postJoinResult.Status}");
                }
            }

            if (joinResult.Status == ActivityResultStatus.Pending)
            {
                // Join not ready yet - pick next pending branch
                return await PickNextBranchOrComplete(context, forkId, cancellationToken);
            }

            return WorkflowExecutionResult.Failed(workflowInstance,
                joinResult.ErrorMessage ?? "Join activity failed");
        }

        // Next activity is another activity within the branch - update branch state and execute it
        workflowInstance.UpdateBranchActivityId(forkId, branchId, nextActivityId);
        await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, nextActivityId,
            cancellationToken: cancellationToken);

        var nextResult = await ExecuteSingleActivityAsync(context, nextActivityDef, null, false, cancellationToken);

        switch (nextResult.Status)
        {
            case ActivityResultStatus.Pending:
                await _lifecycleManager.PauseWorkflowAsync(workflowInstance,
                    $"Branch activity {nextActivityId} requires external completion", cancellationToken);
                await _stateManager.CreateCheckpointAsync(workflowInstance, "Branch activity paused", cancellationToken);
                return WorkflowExecutionResult.Pending(workflowInstance, nextActivityId);

            case ActivityResultStatus.Failed:
                return WorkflowExecutionResult.Failed(workflowInstance, nextResult.ErrorMessage ?? "Branch activity failed");

            case ActivityResultStatus.Completed:
            case ActivityResultStatus.Skipped:
                await _stateManager.CreateCheckpointAsync(workflowInstance, "Branch activity completed", cancellationToken);
                return await HandleBranchActivityCompletionAsync(context, nextActivityDef, nextResult, cancellationToken);

            default:
                return WorkflowExecutionResult.Failed(workflowInstance, $"Unknown status: {nextResult.Status}");
        }
    }

    /// <summary>
    /// Picks the next pending branch activity to work on, or completes if no branches remain.
    /// </summary>
    private async Task<WorkflowExecutionResult> PickNextBranchOrComplete(
        WorkflowExecutionContext context,
        string forkId,
        CancellationToken cancellationToken)
    {
        var workflowInstance = context.WorkflowInstance;
        var remainingBranches = workflowInstance.ActiveBranchActivities
            .Where(b => b.ForkId == forkId && b.Status == "Pending")
            .ToList();

        if (remainingBranches.Any())
        {
            // Pick next pending branch and execute it
            var nextBranch = remainingBranches.First();
            var nextBranchDef = context.Schema.Activities.FirstOrDefault(a => a.Id == nextBranch.ActivityId);

            if (nextBranchDef == null)
                return WorkflowExecutionResult.Failed(workflowInstance, $"Branch activity not found: {nextBranch.ActivityId}");

            await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, nextBranch.ActivityId,
                cancellationToken: cancellationToken);

            var branchResult = await ExecuteSingleActivityAsync(context, nextBranchDef, null, false, cancellationToken);

            switch (branchResult.Status)
            {
                case ActivityResultStatus.Pending:
                    await _lifecycleManager.PauseWorkflowAsync(workflowInstance,
                        $"Branch activity {nextBranch.ActivityId} requires external completion", cancellationToken);
                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Next branch paused", cancellationToken);
                    return WorkflowExecutionResult.Pending(workflowInstance, nextBranch.ActivityId);

                case ActivityResultStatus.Failed:
                    return WorkflowExecutionResult.Failed(workflowInstance, branchResult.ErrorMessage ?? "Branch activity failed");

                case ActivityResultStatus.Completed:
                case ActivityResultStatus.Skipped:
                    await _stateManager.CreateCheckpointAsync(workflowInstance, "Branch activity completed", cancellationToken);
                    var completionResult = await HandleBranchActivityCompletionAsync(context, nextBranchDef, branchResult, cancellationToken);
                    return completionResult ?? WorkflowExecutionResult.Completed(workflowInstance);

                default:
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Unknown status: {branchResult.Status}");
            }
        }

        // No more branches - all branches completed but join not ready yet
        // This means we are waiting for external completions on other branches
        _logger.LogDebug("ENGINE: No more pending branches for fork {ForkId}, waiting", forkId);

        // Check if there are any branches left at all for this fork
        var anyBranches = workflowInstance.ActiveBranchActivities.Any(b => b.ForkId == forkId);
        if (!anyBranches)
        {
            // All branches completed but join returned Pending - this is unexpected
            _logger.LogWarning("ENGINE: All branches for fork {ForkId} completed but join still pending", forkId);
        }

        await _lifecycleManager.PauseWorkflowAsync(workflowInstance,
            "Waiting for remaining branches to complete", cancellationToken);
        await _stateManager.CreateCheckpointAsync(workflowInstance, "Waiting for branches", cancellationToken);

        // Return pending with current activity
        return WorkflowExecutionResult.Pending(workflowInstance, workflowInstance.CurrentActivityId);
    }

    /// <summary>
    /// Records a branch execution result into workflow variables for the join activity to read.
    /// </summary>
    private void RecordBranchResult(
        WorkflowInstance workflowInstance,
        string forkId,
        string branchId,
        ActivityResult activityResult)
    {
        var resultsKey = $"fork_{forkId}_results";
        Dictionary<string, BranchExecutionResult> results;

        if (workflowInstance.Variables.TryGetValue(resultsKey, out var existingObj) &&
            existingObj is Dictionary<string, BranchExecutionResult> existingResults)
        {
            results = existingResults;
        }
        else
        {
            results = new Dictionary<string, BranchExecutionResult>();
        }

        results[branchId] = new BranchExecutionResult
        {
            BranchId = branchId,
            Status = BranchStatus.Completed,
            OutputData = activityResult.OutputData,
            CompletedAt = _dateTimeProvider.ApplicationNow
        };

        workflowInstance.UpdateVariables(new Dictionary<string, object> { [resultsKey] = results });
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