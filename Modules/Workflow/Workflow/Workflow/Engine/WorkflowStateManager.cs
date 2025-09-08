using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Manages workflow state transitions and variable updates with consistent error handling
/// </summary>
public class WorkflowStateManager : IWorkflowStateManager
{
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly ILogger<WorkflowStateManager> _logger;

    public WorkflowStateManager(
        IWorkflowPersistenceService persistenceService,
        ILogger<WorkflowStateManager> logger)
    {
        _persistenceService = persistenceService;
        _logger = logger;
    }

    /// <summary>
    /// Updates workflow variables from activity output data
    /// </summary>
    public async Task<StateUpdateResult> UpdateWorkflowVariablesAsync(
        WorkflowInstance workflowInstance,
        Dictionary<string, object> outputData,
        CancellationToken cancellationToken = default)
    {
        if (!outputData.Any())
        {
            _logger.LogDebug("STATE: No variables to update for workflow {WorkflowInstanceId}",
                workflowInstance.Id);
            return StateUpdateResult.Success();
        }

        try
        {
            _logger.LogDebug("STATE: Updating {Count} variables for workflow {WorkflowInstanceId}",
                outputData.Count, workflowInstance.Id);

            var originalVariables = new Dictionary<string, object>(workflowInstance.Variables);

            workflowInstance.UpdateVariables(outputData);

            var updatedVariables = outputData.Keys.ToList();
            _logger.LogDebug("STATE: Successfully updated variables [{Variables}] for workflow {WorkflowInstanceId}",
                string.Join(", ", updatedVariables), workflowInstance.Id);

            return StateUpdateResult.Success(updatedVariables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATE: Failed to update workflow variables for instance {WorkflowInstanceId}",
                workflowInstance.Id);

            throw new WorkflowStateException($"Variable update failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Updates workflow runtime overrides safely
    /// </summary>
    public async Task<StateUpdateResult> UpdateRuntimeOverridesAsync(
        WorkflowInstance workflowInstance,
        Dictionary<string, RuntimeOverride> runtimeOverrides,
        CancellationToken cancellationToken = default)
    {
        if (!runtimeOverrides.Any())
        {
            _logger.LogDebug("STATE: No runtime overrides to update for workflow {WorkflowInstanceId}",
                workflowInstance.Id);
            return StateUpdateResult.Success();
        }

        try
        {
            _logger.LogDebug("STATE: Updating {Count} runtime overrides for workflow {WorkflowInstanceId}",
                runtimeOverrides.Count, workflowInstance.Id);

            workflowInstance.UpdateRuntimeOverrides(runtimeOverrides);

            var overrideKeys = runtimeOverrides.Keys.ToList();
            _logger.LogDebug(
                "STATE: Successfully updated runtime overrides [{Overrides}] for workflow {WorkflowInstanceId}",
                string.Join(", ", overrideKeys), workflowInstance.Id);

            return StateUpdateResult.Success(overrideKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATE: Failed to update runtime overrides for workflow {WorkflowInstanceId}",
                workflowInstance.Id);

            throw new WorkflowStateException($"Runtime override update failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Safely updates workflow instance with persistence
    /// </summary>
    public async Task<bool> PersistWorkflowInstanceAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("STATE: Persisting workflow instance {WorkflowInstanceId}",
                workflowInstance.Id);

            await _persistenceService.UpdateWorkflowInstanceAsync(workflowInstance, cancellationToken);

            _logger.LogDebug("STATE: Successfully persisted workflow instance {WorkflowInstanceId}",
                workflowInstance.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATE: Failed to persist workflow instance {WorkflowInstanceId}",
                workflowInstance.Id);

            throw new WorkflowStateException($"Workflow persistence failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates workflow state before operations
    /// </summary>
    public StateValidationResult ValidateWorkflowState(
        WorkflowInstance workflowInstance,
        string? expectedActivityId = null,
        WorkflowStatus? requiredStatus = null)
    {
        try
        {
            var validationErrors = new List<string>();

            if (workflowInstance.Status == WorkflowStatus.Failed)
                validationErrors.Add("Workflow is in failed state");

            if (workflowInstance.Status == WorkflowStatus.Completed)
                validationErrors.Add("Workflow is already completed");

            if (requiredStatus.HasValue && workflowInstance.Status != requiredStatus.Value)
                validationErrors.Add($"Workflow must be in {requiredStatus.Value} state");

            if (!string.IsNullOrEmpty(expectedActivityId) &&
                workflowInstance.CurrentActivityId != expectedActivityId)
                validationErrors.Add(
                    $"Expected activity {expectedActivityId}, but current activity is {workflowInstance.CurrentActivityId}");

            var isValid = !validationErrors.Any();

            if (!isValid)
                _logger.LogWarning("STATE: Workflow state validation failed for {WorkflowInstanceId}: {Errors}",
                    workflowInstance.Id, string.Join(", ", validationErrors));

            return new StateValidationResult
            {
                IsValid = isValid,
                ValidationErrors = validationErrors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATE: Error during workflow state validation for {WorkflowInstanceId}",
                workflowInstance.Id);

            return new StateValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Creates strategic checkpoints for long-running workflows
    /// </summary>
    public async Task CreateCheckpointAsync(
        WorkflowInstance workflowInstance,
        string checkpointReason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("STATE: Creating checkpoint for workflow {WorkflowInstanceId}: {Reason}",
                workflowInstance.Id, checkpointReason);

            var existing = await _persistenceService.GetWorkflowInstanceAsync(workflowInstance.Id, cancellationToken);
            // Directly persist the current state
            if (existing is null)
                await _persistenceService.SaveWorkflowInstanceAsync(workflowInstance, cancellationToken);
            else
                await _persistenceService.UpdateWorkflowInstanceAsync(workflowInstance, cancellationToken);

            _logger.LogDebug("STATE: Successfully created checkpoint for workflow {WorkflowInstanceId}",
                workflowInstance.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATE: Failed to create checkpoint for workflow {WorkflowInstanceId}: {Reason}",
                workflowInstance.Id, checkpointReason);
            throw;
        }
    }
}

/// <summary>
/// Result of state update operations
/// </summary>
public class StateUpdateResult
{
    public bool IsSuccess { get; init; }
    public List<string> UpdatedKeys { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public static StateUpdateResult Success(List<string>? updatedKeys = null)
    {
        return new StateUpdateResult { IsSuccess = true, UpdatedKeys = updatedKeys ?? new List<string>() };
    }

    public static StateUpdateResult Failed(string errorMessage)
    {
        return new StateUpdateResult
            { IsSuccess = false, ErrorMessage = errorMessage, UpdatedKeys = new List<string>() };
    }
}

/// <summary>
/// Result of state validation operations
/// </summary>
public class StateValidationResult
{
    public bool IsValid { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
}

/// <summary>
/// Exception thrown when workflow state operations fail
/// </summary>
public class WorkflowStateException : Exception
{
    public WorkflowStateException(string message) : base(message)
    {
    }

    public WorkflowStateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}