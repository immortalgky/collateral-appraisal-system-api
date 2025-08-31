using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Activities.Factories;
using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Services;

namespace Assignment.Workflow.Engine;

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
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowActivityFactory activityFactory,
        IFlowControlManager flowControlManager,
        IWorkflowLifecycleManager lifecycleManager,
        IWorkflowPersistenceService persistenceService,
        ILogger<WorkflowEngine> logger)
    {
        _activityFactory = activityFactory;
        _flowControlManager = flowControlManager;
        _lifecycleManager = lifecycleManager;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public async Task<ActivityResult> ExecuteActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activity = _activityFactory.CreateActivity(activityDefinition.Type);

            _logger.LogDebug("ORCHESTRATION: Executing activity {ActivityId} of type {ActivityType}",
                activityDefinition.Id, activityDefinition.Type);

            var result = await activity.ExecuteAsync(context, cancellationToken);

            _logger.LogDebug("ORCHESTRATION: Activity {ActivityId} execution completed with status {Status}",
                activityDefinition.Id, result.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Failed to execute activity {ActivityId}", activityDefinition.Id);
            return ActivityResult.Failed($"Activity execution failed: {ex.Message}");
        }
    }

    public async Task<ActivityResult> ResumeActivityAsync(
        ActivityDefinition activityDefinition,
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activity = _activityFactory.CreateActivity(activityDefinition.Type);

            _logger.LogDebug("ORCHESTRATION: Resuming activity {ActivityId} of type {ActivityType}",
                activityDefinition.Id, activityDefinition.Type);

            var result = await activity.ResumeAsync(context, resumeInput, cancellationToken);

            _logger.LogDebug("ORCHESTRATION: Activity {ActivityId} resume completed with status {Status}",
                activityDefinition.Id, result.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Failed to resume activity {ActivityId}", activityDefinition.Id);
            return ActivityResult.Failed($"Activity resume failed: {ex.Message}");
        }
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition startActivity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ORCHESTRATION: Starting workflow execution for instance {WorkflowInstanceId}",
                workflowInstance.Id);

            var activitiesToExecute = new Queue<ActivityDefinition>();
            activitiesToExecute.Enqueue(startActivity);

            while (activitiesToExecute.Count > 0)
            {
                var currentActivity = activitiesToExecute.Dequeue();

                var context = CreateActivityContext(workflowInstance, currentActivity);
                ActivityResult result;
                
                try
                {
                    result = await ExecuteActivityAsync(currentActivity, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ORCHESTRATION: Critical error executing activity {ActivityId} in workflow {WorkflowInstanceId}",
                        currentActivity.Id, workflowInstance.Id);
                    
                    await _lifecycleManager.TransitionWorkflowStateAsync(
                        workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                    
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Activity execution error: {ex.Message}");
                }

                // Update workflow variables from the activity output
                try
                {
                    if (result.OutputData.Any())
                    {
                        workflowInstance.UpdateVariables(result.OutputData);
                        await _persistenceService.UpdateWorkflowInstanceAsync(workflowInstance, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ORCHESTRATION: Failed to update workflow variables for instance {WorkflowInstanceId}",
                        workflowInstance.Id);
                    
                    await _lifecycleManager.TransitionWorkflowStateAsync(
                        workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                    
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Variable update error: {ex.Message}");
                }

                // Handle execution result
                WorkflowExecutionResult? executionResult;
                try
                {
                    executionResult = await HandleActivityExecutionResult(
                        workflowSchema, workflowInstance, currentActivity, result, activitiesToExecute, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ORCHESTRATION: Failed to handle activity execution result for activity {ActivityId} in workflow {WorkflowInstanceId}",
                        currentActivity.Id, workflowInstance.Id);
                    
                    await _lifecycleManager.TransitionWorkflowStateAsync(
                        workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                    
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Execution result handling error: {ex.Message}");
                }

                if (executionResult != null)
                {
                    return executionResult;
                }
            }

            // If we get here, workflow completed successfully
            await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);
            
            return WorkflowExecutionResult.Completed(workflowInstance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Workflow execution failed for instance {WorkflowInstanceId}",
                workflowInstance.Id);

            await _lifecycleManager.TransitionWorkflowStateAsync(
                workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);

            return WorkflowExecutionResult.Failed(workflowInstance, ex.Message);
        }
    }

    public async Task<WorkflowExecutionResult> ResumeWorkflowExecutionAsync(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition currentActivity,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "ORCHESTRATION: Resuming workflow execution for instance {WorkflowInstanceId} at activity {ActivityId}",
                workflowInstance.Id, currentActivity.Id);

            var context = CreateActivityContext(workflowInstance, currentActivity);
            ActivityResult result;
            
            try
            {
                result = await ResumeActivityAsync(currentActivity, context, resumeInput, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ORCHESTRATION: Critical error resuming activity {ActivityId} in workflow {WorkflowInstanceId}",
                    currentActivity.Id, workflowInstance.Id);
                
                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                
                return WorkflowExecutionResult.Failed(workflowInstance, $"Activity resume error: {ex.Message}");
            }

            // Update workflow variables from the activity output
            try
            {
                if (result.OutputData.Any())
                {
                    workflowInstance.UpdateVariables(result.OutputData);
                    await _persistenceService.UpdateWorkflowInstanceAsync(workflowInstance, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ORCHESTRATION: Failed to update workflow variables during resume for instance {WorkflowInstanceId}",
                    workflowInstance.Id);
                
                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                
                return WorkflowExecutionResult.Failed(workflowInstance, $"Variable update error during resume: {ex.Message}");
            }

            // Determine next activity using flow control manager
            string? nextActivityId;
            try
            {
                nextActivityId = await _flowControlManager.DetermineNextActivityAsync(
                    workflowSchema, currentActivity.Id, result, workflowInstance.Variables, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ORCHESTRATION: Failed to determine next activity for workflow {WorkflowInstanceId}",
                    workflowInstance.Id);
                
                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                
                return WorkflowExecutionResult.Failed(workflowInstance, $"Flow control error: {ex.Message}");
            }

            if (string.IsNullOrEmpty(nextActivityId))
            {
                // Workflow completed
                try
                {
                    await _lifecycleManager.CompleteWorkflowAsync(workflowInstance, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ORCHESTRATION: Failed to complete workflow {WorkflowInstanceId}",
                        workflowInstance.Id);
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Workflow completion error: {ex.Message}");
                }
                return WorkflowExecutionResult.Completed(workflowInstance);
            }

            // Continue execution through automated activities until reaching a pending state or completion
            var nextActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == nextActivityId);
            if (nextActivity != null)
            {
                try
                {
                    await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, nextActivityId,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ORCHESTRATION: Failed to advance workflow {WorkflowInstanceId} to activity {NextActivityId}",
                        workflowInstance.Id, nextActivityId);
                    
                    await _lifecycleManager.TransitionWorkflowStateAsync(
                        workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);
                    
                    return WorkflowExecutionResult.Failed(workflowInstance, $"Workflow advancement error: {ex.Message}");
                }

                // Execute automated activities in sequence until reaching a TaskActivity or completion
                return await ExecuteWorkflowAsync(workflowSchema, workflowInstance, nextActivity, cancellationToken);
            }

            return WorkflowExecutionResult.Failed(workflowInstance, "Unknown workflow activity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Workflow resume failed for instance {WorkflowInstanceId}",
                workflowInstance.Id);

            await _lifecycleManager.TransitionWorkflowStateAsync(
                workflowInstance, WorkflowStatus.Failed, ex.Message, cancellationToken);

            return WorkflowExecutionResult.Failed(workflowInstance, ex.Message);
        }
    }

    public async Task<bool> ValidateWorkflowDefinitionAsync(
        WorkflowSchema workflowSchema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrEmpty(workflowSchema.Name)) return false;
            if (!workflowSchema.Activities.Any()) return false;

            // Validate transitions using flow control manager
            if (!_flowControlManager.ValidateWorkflowTransitions(workflowSchema))
            {
                return false;
            }

            // Validate each activity
            foreach (var activity in workflowSchema.Activities)
            {
                var workflowActivity = _activityFactory.CreateActivity(activity.Type);
                var context = new ActivityContext
                {
                    ActivityId = activity.Id,
                    Properties = activity.Properties,
                    Variables = workflowSchema.Variables
                };

                var validationResult = await workflowActivity.ValidateAsync(context, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Activity {ActivityId} validation failed: {Errors}",
                        activity.Id, string.Join(", ", validationResult.Errors));
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Error validating workflow definition");
            return false;
        }
    }

    /// <summary>
    /// Creates activity context for workflow execution
    /// </summary>
    private ActivityContext CreateActivityContext(WorkflowInstance workflowInstance,
        ActivityDefinition activityDefinition)
    {
        // Extract runtime override for this specific activity (if any)
        RuntimeOverride? activityRuntimeOverride = null;
        if (workflowInstance.RuntimeOverrides.TryGetValue(activityDefinition.Id, out var runtimeOverride))
        {
            activityRuntimeOverride = runtimeOverride;
            _logger.LogDebug("Found runtime override for activity {ActivityId}: {Override}",
                activityDefinition.Id, runtimeOverride.OverrideReason);
        }

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = activityDefinition.Id,
            Properties = activityDefinition.Properties,
            Variables = workflowInstance.Variables,
            InputData = new Dictionary<string, object>(),
            CurrentAssignee = workflowInstance.CurrentAssignee,
            WorkflowInstance = workflowInstance,
            RuntimeOverrides = activityRuntimeOverride
        };
    }

    /// <summary>
    /// Handles the result of activity execution and determines workflow flow
    /// </summary>
    private async Task<WorkflowExecutionResult?> HandleActivityExecutionResult(
        WorkflowSchema workflowSchema,
        WorkflowInstance workflowInstance,
        ActivityDefinition currentActivity,
        ActivityResult result,
        Queue<ActivityDefinition> activitiesToExecute,
        CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case ActivityResultStatus.Completed:
                // Determine next activity using flow control manager
                var nextActivityId = await _flowControlManager.DetermineNextActivityAsync(
                    workflowSchema, currentActivity.Id, result, workflowInstance.Variables, cancellationToken);

                if (!string.IsNullOrEmpty(nextActivityId))
                {
                    var nextActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == nextActivityId);
                    if (nextActivity != null)
                    {
                        await _lifecycleManager.AdvanceWorkflowAsync(workflowInstance, nextActivityId,
                            cancellationToken: cancellationToken);
                        
                        activitiesToExecute.Enqueue(nextActivity);
                    }
                }

                // If nextActivityId is null, the workflow will complete naturally
                break;

            case ActivityResultStatus.Failed:
                await _lifecycleManager.TransitionWorkflowStateAsync(
                    workflowInstance, WorkflowStatus.Failed, result.ErrorMessage, cancellationToken);
                return WorkflowExecutionResult.Failed(workflowInstance, result.ErrorMessage ?? "Activity failed");

            case ActivityResultStatus.Pending:
                // Activity requires external completion
                _logger.LogInformation("ORCHESTRATION: Activity {ActivityId} requires external completion",
                    currentActivity.Id);
                return WorkflowExecutionResult.Pending(workflowInstance, currentActivity.Id);

            default:
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Unknown activity result status: {result.Status}");
        }

        return null; // Continue execution
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
            _logger.LogInformation("ORCHESTRATION: Starting complete workflow for definition {WorkflowDefinitionId}",
                workflowDefinitionId);

            // 1. Load workflow schema via persistence service
            var workflowSchema =
                await _persistenceService.GetWorkflowSchemaAsync(workflowDefinitionId, cancellationToken);
            if (workflowSchema == null)
            {
                return WorkflowExecutionResult.Failed(null, $"Workflow definition not found: {workflowDefinitionId}");
            }

            // 2. Initialize a workflow instance via lifecycle manager
            var workflowInstance = await _lifecycleManager.InitializeWorkflowAsync(
                workflowDefinitionId, workflowSchema, instanceName, startedBy, initialVariables, correlationId,
                assignmentOverrides, cancellationToken);

            // 3. Get start activity
            var startActivity = _flowControlManager.GetStartActivity(workflowSchema);

            // 4. Execute a workflow via an existing orchestration method
            var executionResult =
                await ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity, cancellationToken);

            _logger.LogInformation("ORCHESTRATION: Complete workflow startup finished with status {Status}",
                executionResult.Status);

            return executionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ORCHESTRATION: Failed to start complete workflow for definition {WorkflowDefinitionId}",
                workflowDefinitionId);

            return WorkflowExecutionResult.Failed(null, ex.Message);
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
            {
                return WorkflowExecutionResult.Failed(null, $"Workflow instance not found: {workflowInstanceId}");
            }

            var workflowSchema =
                await _persistenceService.GetWorkflowSchemaAsync(workflowInstance.WorkflowDefinitionId,
                    cancellationToken);
            if (workflowSchema == null)
            {
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Workflow definition not found: {workflowInstance.WorkflowDefinitionId}");
            }

            // 2. Validate current activity
            if (workflowInstance.CurrentActivityId != activityId)
            {
                return WorkflowExecutionResult.Failed(workflowInstance,
                    $"Activity {activityId} is not the current activity");
            }

            var currentActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == activityId);
            if (currentActivity == null)
            {
                return WorkflowExecutionResult.Failed(workflowInstance, $"Activity definition not found: {activityId}");
            }

            // 3. Update workflow instance with new runtime overrides if provided
            if (nextAssignmentOverrides != null && nextAssignmentOverrides.Any())
            {
                workflowInstance.UpdateRuntimeOverrides(nextAssignmentOverrides);
                _logger.LogDebug("ORCHESTRATION: Updated workflow instance {WorkflowInstanceId} with {Count} runtime overrides",
                    workflowInstanceId, nextAssignmentOverrides.Count);
            }

            // 4. Resume the workflow via the existing orchestration method
            var executionResult = await ResumeWorkflowExecutionAsync(workflowSchema, workflowInstance, currentActivity,
                input ?? new Dictionary<string, object>(), cancellationToken);

            _logger.LogInformation("ORCHESTRATION: Complete workflow resume finished with status {Status}",
                executionResult.Status);

            return executionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATION: Failed to resume complete workflow for instance {WorkflowInstanceId}",
                workflowInstanceId);

            return WorkflowExecutionResult.Failed(null, ex.Message);
        }
    }
}