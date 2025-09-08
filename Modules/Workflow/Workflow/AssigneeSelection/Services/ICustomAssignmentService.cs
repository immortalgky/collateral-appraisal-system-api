namespace Workflow.AssigneeSelection.Services;

/// <summary>
/// Interface for custom assignment services that can implement unlimited business logic
/// for determining task assignment based on workflow context and variables
/// </summary>
public interface ICustomAssignmentService
{
    /// <summary>
    /// Determines custom assignment based on workflow context and variables
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance identifier</param>
    /// <param name="activityId">The activity identifier</param>
    /// <param name="workflowVariables">Current workflow variables for decision making</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Custom assignment result indicating whether to use custom assignment and details</returns>
    Task<CustomAssignmentResult> GetAssignmentContextAsync(
        string workflowInstanceId, 
        string activityId, 
        Dictionary<string, object> workflowVariables,
        CancellationToken cancellationToken = default);
}