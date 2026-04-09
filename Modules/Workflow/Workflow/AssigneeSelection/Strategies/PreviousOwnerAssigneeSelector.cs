using Microsoft.EntityFrameworkCore;
using Workflow.AssigneeSelection.Core;
using Workflow.Data;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to the user who completed this activity previously (route-back scenario).
/// Queries WorkflowActivityExecutions for the most recent completed assignee.
/// </summary>
public class PreviousOwnerAssigneeSelector : IAssigneeSelector
{
    private readonly WorkflowDbContext _dbContext;
    private readonly ILogger<PreviousOwnerAssigneeSelector> _logger;

    public PreviousOwnerAssigneeSelector(WorkflowDbContext dbContext, ILogger<PreviousOwnerAssigneeSelector> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.WorkflowInstanceId == Guid.Empty)
            {
                return AssigneeSelectionResult.Failure(
                    "PreviousOwner strategy requires a valid WorkflowInstanceId");
            }

            var previousOwner = await _dbContext.WorkflowActivityExecutions
                .Where(ae => ae.WorkflowInstanceId == context.WorkflowInstanceId
                             && ae.ActivityId == context.ActivityName
                             && ae.Status == ActivityExecutionStatus.Completed
                             && ae.CompletedBy != null
                             && ae.CompletedBy != ""
                             && ae.CompletedBy != "system")
                .OrderByDescending(ae => ae.CompletedOn)
                .Select(ae => ae.CompletedBy)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrEmpty(previousOwner))
            {
                _logger.LogInformation(
                    "No previous owner found for activity {ActivityId} in workflow {WorkflowInstanceId}",
                    context.ActivityName, context.WorkflowInstanceId);
                return AssigneeSelectionResult.Failure(
                    $"No previous owner found for activity '{context.ActivityName}'");
            }

            _logger.LogInformation(
                "PreviousOwner selector assigned {UserId} for activity {ActivityName} in workflow {WorkflowInstanceId}",
                previousOwner, context.ActivityName, context.WorkflowInstanceId);

            return AssigneeSelectionResult.Success(previousOwner, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "PreviousOwner",
                ["PreviouslyCompletedBy"] = previousOwner,
                ["RouteBackAssignment"] = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during previous owner selection for activity {ActivityName}",
                context.ActivityName);
            return AssigneeSelectionResult.Failure($"Selection failed: {ex.Message}");
        }
    }
}
