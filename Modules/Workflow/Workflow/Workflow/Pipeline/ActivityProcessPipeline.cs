using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Loads config rows for an activity, resolves each step from DI, and runs them sequentially.
/// Stops on first failure. No config rows = pass-through (existing behavior preserved).
/// </summary>
public class ActivityProcessPipeline(
    WorkflowDbContext dbContext,
    IWorkflowInstanceRepository workflowInstanceRepository,
    ProcessStepResolver stepResolver,
    ILogger<ActivityProcessPipeline> logger) : IActivityProcessPipeline
{
    public async Task<ProcessStepResult> ExecuteAsync(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        Dictionary<string, object> input,
        CancellationToken ct)
    {
        // Load active config rows for this activity, ordered by SortOrder
        var configs = await dbContext.ActivityProcessConfigurations
            .Where(c => c.ActivityName == activityId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        if (configs.Count == 0)
        {
            logger.LogDebug("No process configurations found for activity {ActivityId}, passing through", activityId);
            return ProcessStepResult.Ok();
        }

        // Load workflow instance for CorrelationId + Variables
        var workflowInstance = await workflowInstanceRepository.GetByIdAsync(workflowInstanceId, ct)
                               ?? throw new InvalidOperationException(
                                   $"Workflow instance {workflowInstanceId} not found");

        // CorrelationId (requestId) — always available
        var correlationId = Guid.TryParse(workflowInstance.CorrelationId, out var cid) ? cid : Guid.Empty;

        // AppraisalId from Variables — null before appraisal creation
        Guid? appraisalId = ResolveAppraisalId(workflowInstance.Variables);

        // Run each step sequentially
        foreach (var config in configs)
        {
            var step = stepResolver.Resolve(config.ProcessorName);
            if (step is null)
            {
                logger.LogWarning(
                    "Process step '{ProcessorName}' not found for activity {ActivityId}, skipping",
                    config.ProcessorName, activityId);
                continue;
            }

            var context = new ProcessStepContext
            {
                CorrelationId = correlationId,
                AppraisalId = appraisalId,
                WorkflowInstanceId = workflowInstanceId,
                ActivityName = activityId,
                CompletedBy = completedBy,
                Input = input,
                Variables = workflowInstance.Variables,
                Parameters = config.Parameters
            };

            logger.LogInformation(
                "Executing process step '{StepName}' ({ProcessorName}) for activity {ActivityId}",
                config.StepName, config.ProcessorName, activityId);

            var result = await step.ExecuteAsync(context, ct);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Process step '{StepName}' failed for activity {ActivityId}: {Errors}",
                    config.StepName, activityId, string.Join("; ", result.Errors));
                return result;
            }
        }

        return ProcessStepResult.Ok();
    }

    private static Guid? ResolveAppraisalId(Dictionary<string, object>? variables)
    {
        if (variables is null || !variables.TryGetValue("appraisalId", out var aidObj))
            return null;

        return aidObj switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            JsonElement je when je.ValueKind == JsonValueKind.String
                && Guid.TryParse(je.GetString(), out var jp) => jp,
            _ => null
        };
    }
}
