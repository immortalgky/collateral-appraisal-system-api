using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Services;

/// <summary>
/// ENHANCEMENT: Orchestrates workflow execution following "one step = one transaction" rule
/// Each workflow step is executed in its own atomic transaction boundary
/// </summary>
public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IWorkflowEngine _engine;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowSchemaValidator _schemaValidator;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(
        IWorkflowEngine engine,
        IWorkflowPersistenceService persistenceService,
        IWorkflowSchemaValidator schemaValidator,
        ILogger<WorkflowOrchestrator> logger)
    {
        _engine = engine;
        _persistenceService = persistenceService;
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    public async Task<WorkflowExecutionResult> ExecuteCompleteWorkflowAsync(
        Guid workflowInstanceId,
        int maxSteps = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ORCHESTRATOR: Starting complete workflow execution for {WorkflowInstanceId} with max {MaxSteps} steps",
            workflowInstanceId, maxSteps);

        var workflowInstance = await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null)
            return WorkflowExecutionResult.Failed(null, $"Workflow instance {workflowInstanceId} not found");

        var currentActivityId = workflowInstance.CurrentActivityId;
        var stepCount = 0;
        WorkflowExecutionResult lastResult = WorkflowExecutionResult.Running(workflowInstance, currentActivityId);

        // Execute steps one at a time, each in its own transaction
        while (stepCount < maxSteps && 
               (lastResult.Status == WorkflowExecutionStatus.Running || 
                lastResult.Status == WorkflowExecutionStatus.StepCompleted))
        {
            stepCount++;

            _logger.LogDebug("ORCHESTRATOR: Executing step {StepCount} for activity {ActivityId}",
                stepCount, currentActivityId);

            // Execute single atomic step
            lastResult = await ExecuteSingleStepAsync(
                workflowInstanceId, 
                currentActivityId, 
                null, 
                false, 
                cancellationToken);

            if (lastResult.Status == WorkflowExecutionStatus.StepCompleted)
            {
                // Move to next activity
                currentActivityId = lastResult.NextActivityId!;
                _logger.LogDebug("ORCHESTRATOR: Step {StepCount} completed, next activity: {NextActivityId}",
                    stepCount, currentActivityId);
            }
            else if (lastResult.Status == WorkflowExecutionStatus.Pending)
            {
                _logger.LogInformation("ORCHESTRATOR: Workflow paused at step {StepCount}, requires external completion",
                    stepCount);
                break;
            }
            else if (lastResult.Status == WorkflowExecutionStatus.Completed)
            {
                _logger.LogInformation("ORCHESTRATOR: Workflow completed successfully after {StepCount} steps",
                    stepCount);
                break;
            }
            else if (lastResult.Status == WorkflowExecutionStatus.Failed)
            {
                _logger.LogError("ORCHESTRATOR: Workflow failed at step {StepCount}: {ErrorMessage}",
                    stepCount, lastResult.ErrorMessage);
                break;
            }
        }

        if (stepCount >= maxSteps)
        {
            _logger.LogWarning("ORCHESTRATOR: Workflow execution stopped after {MaxSteps} steps to prevent infinite loops",
                maxSteps);
            return WorkflowExecutionResult.Failed(workflowInstance, 
                $"Maximum steps ({maxSteps}) exceeded - possible infinite loop");
        }

        return lastResult;
    }

    public async Task<WorkflowExecutionResult> ContinueWorkflowExecutionAsync(
        Guid workflowInstanceId,
        string fromActivityId,
        Dictionary<string, object>? input = null,
        int maxSteps = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ORCHESTRATOR: Continuing workflow execution for {WorkflowInstanceId} from {ActivityId}",
            workflowInstanceId, fromActivityId);

        // First step is a resume with input
        var firstStepResult = await ExecuteSingleStepAsync(
            workflowInstanceId,
            fromActivityId,
            input,
            true, // isResume = true
            cancellationToken);

        if (firstStepResult.Status == WorkflowExecutionStatus.Completed ||
            firstStepResult.Status == WorkflowExecutionStatus.Failed ||
            firstStepResult.Status == WorkflowExecutionStatus.Pending)
        {
            // Workflow finished or paused after resume step
            return firstStepResult;
        }

        if (firstStepResult.Status == WorkflowExecutionStatus.StepCompleted && 
            firstStepResult.NextActivityId != null)
        {
            // Continue with remaining steps
            return await ExecuteCompleteWorkflowAsync(workflowInstanceId, maxSteps - 1, cancellationToken);
        }

        return firstStepResult;
    }

    public async Task<WorkflowExecutionResult> ExecuteSingleStepAsync(
        Guid workflowInstanceId,
        string activityId,
        Dictionary<string, object>? input = null,
        bool isResume = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ORCHESTRATOR: Executing single step for {WorkflowInstanceId} at {ActivityId} (resume: {IsResume})",
            workflowInstanceId, activityId, isResume);

        try
        {
            // Load workflow instance and schema
            var workflowInstance = await _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, cancellationToken);
            if (workflowInstance == null)
                return WorkflowExecutionResult.Failed(null, $"Workflow instance {workflowInstanceId} not found");

            var workflowSchema = await _persistenceService.GetWorkflowSchemaAsync(workflowInstance.WorkflowDefinitionId, cancellationToken);
            if (workflowSchema == null)
                return WorkflowExecutionResult.Failed(workflowInstance, "Workflow schema not found");

            // Find the activity to execute
            var activity = workflowSchema.Activities.FirstOrDefault(a => a.Id == activityId);
            if (activity == null)
                return WorkflowExecutionResult.Failed(workflowInstance, $"Activity {activityId} not found in workflow schema");

            // Execute single step via engine (which now only processes one activity)
            return await _engine.ExecuteWorkflowAsync(
                workflowSchema,
                workflowInstance,
                activity,
                input,
                isResume,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORCHESTRATOR: Critical error during single step execution for {WorkflowInstanceId} at {ActivityId}",
                workflowInstanceId, activityId);

            return WorkflowExecutionResult.Failed(null, $"Step execution failed: {ex.Message}");
        }
    }
}