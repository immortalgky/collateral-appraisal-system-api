using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Engine;

/// <summary>
/// Engine for executing cascading assignment strategies with fallback support
/// </summary>
public interface ICascadingAssignmentEngine
{
    /// <summary>
    /// Executes a cascading assignment strategy chain
    /// </summary>
    /// <param name="context">Assignment context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assignment result from the first successful strategy</returns>
    Task<AssigneeSelectionResult> ExecuteAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a task is a route-back scenario by checking execution history
    /// </summary>
    /// <param name="workflowInstanceId">Workflow instance ID</param>
    /// <param name="activityId">Activity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if this is a route-back scenario</returns>
    Task<bool> IsRouteBackScenarioAsync(
        Guid workflowInstanceId,
        string activityId,
        CancellationToken cancellationToken = default);
}