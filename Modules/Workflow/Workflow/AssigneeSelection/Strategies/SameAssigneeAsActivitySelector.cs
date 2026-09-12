using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workflow.AssigneeSelection.Core;
using Workflow.Data;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns the task to the same user who was assigned a different, named prior activity in the
/// same workflow instance. This is the positive counterpart of the <c>excludeAssigneesFrom</c>
/// rule: instead of excluding that activity's assignee, it selects them. The source activity id is
/// read from <c>Properties["sameAssigneeAsActivity"]</c> and resolved against
/// <see cref="WorkflowActivityExecution.AssignedTo"/> — the same identifier
/// <c>excludeAssigneesFrom</c> compares against — so the two rules are exact inverses.
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
        var sourceActivityId = GetStringValue(context.Properties, "sameAssigneeAsActivity");

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
                         && ae.AssignedTo != null
                         && ae.AssignedTo != ""
                         && ae.AssignedTo != "system")
            .OrderByDescending(ae => ae.CompletedOn)
            .Select(ae => ae.AssignedTo)
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

    private static string? GetStringValue(Dictionary<string, object>? dict, string key)
    {
        if (dict is null || !dict.TryGetValue(key, out var val))
            return null;

        if (val is string s) return s;
        if (val is JsonElement { ValueKind: JsonValueKind.String } je) return je.GetString();
        return val?.ToString();
    }
}
