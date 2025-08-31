using Assignment.AssigneeSelection.Core;
using Assignment.Workflow.Models;

namespace Assignment.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to the user who completed this activity previously (route-back scenario)
/// </summary>
public class PreviousOwnerAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<PreviousOwnerAssigneeSelector> _logger;

    public PreviousOwnerAssigneeSelector(ILogger<PreviousOwnerAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workflowInstanceId = GetWorkflowInstanceIdFromContext(context);
            var activityId = GetActivityIdFromContext(context);

            if (workflowInstanceId == null || string.IsNullOrEmpty(activityId))
            {
                return AssigneeSelectionResult.Failure(
                    "PreviousOwner strategy requires workflow instance ID and activity ID");
            }

            var previousOwner = await FindPreviousOwnerAsync(workflowInstanceId.Value, activityId, cancellationToken);

            if (string.IsNullOrEmpty(previousOwner))
            {
                return AssigneeSelectionResult.Failure(
                    $"No previous owner found for activity '{activityId}' in workflow instance '{workflowInstanceId}'");
            }

            var isEligible = await ValidateAssigneeEligibilityAsync(previousOwner, context, cancellationToken);

            if (!isEligible)
            {
                return AssigneeSelectionResult.Failure(
                    $"Previous owner '{previousOwner}' is not eligible for assignment");
            }

            _logger.LogInformation("PreviousOwner selector assigned user {UserId} for activity {ActivityName}",
                previousOwner, context.ActivityName);

            return AssigneeSelectionResult.Success(previousOwner, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "PreviousOwner",
                ["PreviouslyCompletedBy"] = previousOwner,
                ["RouteBackAssignment"] = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during previous owner assignee selection");
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }

    private Guid? GetWorkflowInstanceIdFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("WorkflowInstanceId", out var instanceId) == true)
        {
            if (instanceId is Guid guid)
                return guid;
            
            if (Guid.TryParse(instanceId?.ToString(), out guid))
                return guid;
        }

        return null;
    }

    private string? GetActivityIdFromContext(AssignmentContext context)
    {
        if (context.Properties?.TryGetValue("ActivityId", out var activityId) == true)
        {
            return activityId?.ToString();
        }

        return null;
    }

    private async Task<string?> FindPreviousOwnerAsync(Guid workflowInstanceId, string activityId, CancellationToken cancellationToken)
    {
        // Find the most recent person who completed this activity in this workflow instance
        // This implements the "if handled by someone before, assign to them" logic
        
        // Note: We need access to the database context to implement this
        // For now, this will return null until we inject the DbContext
        // The calling code should handle null gracefully and fall back to strategies
        
        _logger.LogInformation("Searching for previous owner of activity {ActivityId} in workflow {WorkflowInstanceId}", 
            activityId, workflowInstanceId);
        
        // TODO: Inject AssignmentDbContext to implement this query:
        // var previousExecution = await _dbContext.WorkflowActivityExecutions
        //     .Where(ae => ae.WorkflowInstanceId == workflowInstanceId 
        //                 && ae.ActivityId == activityId 
        //                 && ae.Status == ActivityExecutionStatus.Completed
        //                 && !string.IsNullOrEmpty(ae.CompletedBy))
        //     .OrderByDescending(ae => ae.CompletedOn)
        //     .FirstOrDefaultAsync(cancellationToken);
        // 
        // if (previousExecution != null)
        // {
        //     _logger.LogInformation("Found previous owner {UserId} for activity {ActivityId}", 
        //         previousExecution.CompletedBy, activityId);
        //     return previousExecution.CompletedBy;
        // }

        _logger.LogInformation("No previous owner found for activity {ActivityId}", activityId);
        return null;
    }

    private async Task<bool> ValidateAssigneeEligibilityAsync(string assigneeId, AssignmentContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // TODO: Implement actual validation logic here.
        // Could be extended to check:
        // - User exists and is active
        // - User has required role/permissions
        // - User is not overloaded
        // - User is available (not on leave, etc.)

        return !string.IsNullOrWhiteSpace(assigneeId);
    }
}