using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Interface for managing workflow state transitions and variable updates
/// </summary>
public interface IWorkflowStateManager
{
    /// <summary>
    /// Updates workflow variables from activity output data
    /// </summary>
    Task<StateUpdateResult> UpdateWorkflowVariablesAsync(
        WorkflowInstance workflowInstance,
        Dictionary<string, object> outputData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates workflow runtime overrides safely
    /// </summary>
    Task<StateUpdateResult> UpdateRuntimeOverridesAsync(
        WorkflowInstance workflowInstance,
        Dictionary<string, RuntimeOverride> runtimeOverrides,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely updates workflow instance with persistence
    /// </summary>
    Task<bool> PersistWorkflowInstanceAsync(
        WorkflowInstance workflowInstance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates workflow state before operations
    /// </summary>
    StateValidationResult ValidateWorkflowState(
        WorkflowInstance workflowInstance,
        string? expectedActivityId = null,
        WorkflowStatus? requiredStatus = null);

    /// <summary>
    /// Creates strategic checkpoints for long-running workflows
    /// </summary>
    Task CreateCheckpointAsync(
        WorkflowInstance workflowInstance,
        string checkpointReason,
        CancellationToken cancellationToken = default);
}