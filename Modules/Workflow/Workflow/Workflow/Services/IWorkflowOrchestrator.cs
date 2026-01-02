using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Services;

/// <summary>
/// ENHANCEMENT: Orchestrates multi-step workflow execution following "one step = one transaction" rule
/// Coordinates between individual atomic steps while maintaining transaction boundaries
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// Execute a complete workflow by orchestrating individual steps
    /// Each step is executed in its own transaction boundary
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteCompleteWorkflowAsync(
        Guid workflowInstanceId,
        int maxSteps = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continue workflow execution from a specific point
    /// Used for resuming workflows that had pending activities
    /// </summary>
    Task<WorkflowExecutionResult> ContinueWorkflowExecutionAsync(
        Guid workflowInstanceId,
        string fromActivityId,
        Dictionary<string, object>? input = null,
        int maxSteps = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a single workflow step atomically
    /// Follows the "one step = one transaction" principle
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteSingleStepAsync(
        Guid workflowInstanceId,
        string activityId,
        Dictionary<string, object>? input = null,
        bool isResume = false,
        CancellationToken cancellationToken = default);
}