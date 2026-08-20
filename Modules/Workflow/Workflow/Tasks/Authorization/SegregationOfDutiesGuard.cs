using Microsoft.EntityFrameworkCore;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.Data;
using Workflow.Services.Configuration;
using Workflow.Workflow.Models;

namespace Workflow.Tasks.Authorization;

/// <summary>
/// Enforces <c>excludeAssigneesFrom</c> for individuals taking a task, which the assignment pipeline
/// cannot do for pool tasks.
///
/// <para>
/// <see cref="AssigneeSelection.Pipeline.ExclusionFilter"/> removes excluded users from the candidate
/// pool, which works when a specific person is selected. It has no effect on a <c>pool</c> assignment:
/// <see cref="AssigneeSelection.Strategies.PoolAssigneeSelector"/> emits a <em>group</em> name and never
/// consults the filtered candidate list, so every group member can still reach the task. This guard
/// re-applies the rule at the moment an individual claims or opens it.
/// </para>
/// </summary>
public interface ISegregationOfDutiesGuard
{
    /// <summary>
    /// Returns the id of the activity that disqualifies <paramref name="username"/> from working
    /// <paramref name="activityId"/>, or <c>null</c> when they are allowed.
    /// </summary>
    Task<string?> GetBlockingActivityAsync(
        Guid workflowInstanceId,
        string activityId,
        string username,
        CancellationToken cancellationToken = default);
}

public class SegregationOfDutiesGuard(
    WorkflowDbContext dbContext,
    ITaskConfigurationService configurationService,
    ILogger<SegregationOfDutiesGuard> logger) : ISegregationOfDutiesGuard
{
    public async Task<string?> GetBlockingActivityAsync(
        Guid workflowInstanceId,
        string activityId,
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(activityId))
            return null;

        var instance = await dbContext.WorkflowInstances
            .Include(i => i.WorkflowDefinition)
            .Include(i => i.ActivityExecutions)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == workflowInstanceId, cancellationToken);

        if (instance is null)
            return null;

        var excludeFrom = await ResolveExcludeAssigneesFromAsync(instance, activityId, cancellationToken);
        if (excludeFrom.Count == 0)
            return null;

        // CompletedBy is the individual who actually finished the activity — the same identifier shape
        // PreviousOwnerAssigneeSelector compares against. AssignedTo is not used: for a pool task it
        // holds a group name, not a person.
        var blocking = instance.ActivityExecutions
            .Where(e => e.Status == ActivityExecutionStatus.Completed
                        && !string.IsNullOrEmpty(e.CompletedBy)
                        && excludeFrom.Contains(e.ActivityId)
                        && string.Equals(e.CompletedBy, username, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ActivityId)
            .FirstOrDefault();

        if (blocking is not null)
            logger.LogInformation(
                "Segregation of duties: {Username} completed {BlockingActivity} on instance {InstanceId} and cannot work {ActivityId}",
                username, blocking, workflowInstanceId, activityId);

        return blocking;
    }

    /// <summary>
    /// Effective exclusion list, using the same precedence as the assignment pipeline: the DB override
    /// wins when its column is set, otherwise the workflow definition JSON is the baseline.
    /// </summary>
    private async Task<HashSet<string>> ResolveExcludeAssigneesFromAsync(
        WorkflowInstance instance,
        string activityId,
        CancellationToken cancellationToken)
    {
        var bankingSegment = instance.Variables.TryGetValue("bankingSegment", out var seg)
            ? seg?.ToString()
            : null;

        var config = await configurationService.GetConfigurationAsync(
            activityId,
            instance.WorkflowDefinitionId.ToString(),
            bankingSegment,
            cancellationToken);

        if (config?.ExcludeAssigneesFrom is { } fromDb)
            return new HashSet<string>(fromDb, StringComparer.OrdinalIgnoreCase);

        var properties = ActivityPropertiesExtractor.Extract(instance, activityId, logger);
        var rules = ActivityAssignmentRules.Parse(properties, logger, activityId);

        return new HashSet<string>(rules.ExcludeAssigneesFrom, StringComparer.OrdinalIgnoreCase);
    }
}
