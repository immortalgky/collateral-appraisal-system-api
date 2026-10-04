using Microsoft.EntityFrameworkCore;
using Workflow.AssigneeSelection.Core;
using Workflow.Data;
using Workflow.Workflow;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns the task to the same user who completed a different, named prior activity in the
/// same workflow instance. The source activity id is read from
/// <c>Properties["sameAssigneeAsActivity"]</c> and resolved against
/// <see cref="WorkflowActivityExecution.CompletedBy"/> (newest completion first, same filters as
/// <see cref="PreviousOwnerAssigneeSelector"/>). <c>AssignedTo</c> is not used: it holds whatever the
/// instance's <c>CurrentAssignee</c> was when the execution was created (often null, a previous holder or a group
/// label), while the person who did the work is recorded in <c>CompletedBy</c>.
/// It is the positive counterpart of <c>excludeAssigneesFrom</c>: ExclusionFilter reads the same
/// identifier (<c>CompletedBy</c>, see <c>AssignmentContextBuilder.BuildPriorAssigneesMap</c>).
/// Fails softly (so the cascade falls through) when the property is missing or the source activity
/// has no completed execution in this instance (e.g. non-PMA flows where it never ran).
/// </summary>
public class SameAssigneeAsActivitySelector : IAssigneeSelector
{
    private readonly WorkflowDbContext _dbContext;
    private readonly ILogger<SameAssigneeAsActivitySelector> _logger;

    public SameAssigneeAsActivitySelector(
        WorkflowDbContext dbContext,
        ILogger<SameAssigneeAsActivitySelector> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceActivityId = context.Properties is null
            ? null
            : JsonPropertyReader.GetString(context.Properties, JsonPropertyReader.SameAssigneeAsActivityKey);

        if (string.IsNullOrWhiteSpace(sourceActivityId))
        {
            _logger.LogInformation(
                "SameAssigneeAsActivity selector skipped for activity {ActivityName}: no 'sameAssigneeAsActivity' configured",
                context.ActivityName);
            return AssigneeSelectionResult.Failure(
                "SameAssigneeAsActivity strategy requires 'sameAssigneeAsActivity' in properties");
        }

        if (context.WorkflowInstanceId == Guid.Empty)
        {
            return AssigneeSelectionResult.Failure(
                "SameAssigneeAsActivity strategy requires a valid WorkflowInstanceId");
        }

        var assignee = await _dbContext.WorkflowActivityExecutions
            .Where(ae => ae.WorkflowInstanceId == context.WorkflowInstanceId
                         && ae.ActivityId == sourceActivityId
                         && ae.Status == ActivityExecutionStatus.Completed
                         && ae.CompletedBy != null
                         && ae.CompletedBy != ""
                         && ae.CompletedBy != "system")
            .OrderByDescending(ae => ae.CompletedOn)
            .ThenByDescending(ae => ae.Id)
            .Select(ae => ae.CompletedBy)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(assignee))
        {
            _logger.LogInformation(
                "SameAssigneeAsActivity selector: no completed assignee for source activity {SourceActivity} in workflow {WorkflowInstanceId}",
                sourceActivityId, context.WorkflowInstanceId);
            return AssigneeSelectionResult.Failure(
                $"No completed assignee found for source activity '{sourceActivityId}'");
        }

        _logger.LogInformation(
            "SameAssigneeAsActivity selector assigned {UserId} for activity {ActivityName} from source activity {SourceActivity} in workflow {WorkflowInstanceId}",
            assignee, context.ActivityName, sourceActivityId, context.WorkflowInstanceId);

        return AssigneeSelectionResult.Success(assignee, new Dictionary<string, object>
        {
            ["SelectionStrategy"] = "SameAssigneeAsActivity",
            ["SourceActivity"] = sourceActivityId,
            ["ResolvedAssignee"] = assignee
        });
    }
}
