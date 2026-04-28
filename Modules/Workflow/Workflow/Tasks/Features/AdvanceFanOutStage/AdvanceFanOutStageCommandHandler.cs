using System.Text.Json;
using Shared.Identity;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.AdvanceFanOutStage;

/// <summary>
/// Dispatches a user action on a fan-out PendingTask.
///
/// Stage-transition path (<c>to:</c>): advances FanOutItemState + reassigns PendingTask in-place.
/// Terminal path (<c>complete:</c>): delegates to ResumeWorkflowAsync (standard fan-out complete path).
/// Legacy path (no <c>stages[]</c>): delegates to ResumeWorkflowAsync unchanged.
/// </summary>
public class AdvanceFanOutStageCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowInstanceRepository instanceRepository,
    IAssignmentRepository assignmentRepository,
    IWorkflowService workflowService,
    FanOutTaskActivity fanOutActivity,
    ICurrentUserService currentUser,
    ILogger<AdvanceFanOutStageCommandHandler> logger)
    : ICommandHandler<AdvanceFanOutStageCommand, AdvanceFanOutStageResult>
{
    public async Task<AdvanceFanOutStageResult> Handle(
        AdvanceFanOutStageCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the PendingTask — taskId, instance triple, or correlation triple.
        // The correlation-id fallback lets quotation features pass the QuotationRequest.Id
        // (which is also the child workflow's CorrelationId per the spawn consumer) without
        // needing the child WorkflowInstanceId — that field isn't always denormalized onto
        // the parent aggregate.
        PendingTask? task;
        if (command.PendingTaskId.HasValue)
        {
            task = await dbContext.PendingTasks.FindAsync([command.PendingTaskId.Value], cancellationToken);
        }
        else if (command.WorkflowInstanceId.HasValue
                 && !string.IsNullOrEmpty(command.ActivityId)
                 && command.CompanyId.HasValue)
        {
            task = await assignmentRepository.GetFanOutTaskByCompanyAsync(
                command.WorkflowInstanceId.Value, command.ActivityId, command.CompanyId.Value, cancellationToken);

            // Fallback: caller passed a quotationId (or any other domain id) as
            // WorkflowInstanceId. Try resolving by CorrelationId.
            if (task is null)
            {
                task = await assignmentRepository.GetFanOutTaskByCorrelationIdAndCompanyAsync(
                    command.WorkflowInstanceId.Value, command.ActivityId, command.CompanyId.Value, cancellationToken);
            }
        }
        else
        {
            return new AdvanceFanOutStageResult(false,
                "Provide either PendingTaskId or (WorkflowInstanceId + ActivityId + CompanyId).");
        }

        if (task is null)
            return new AdvanceFanOutStageResult(false, "PendingTask not found");

        if (!task.AssigneeCompanyId.HasValue)
            return new AdvanceFanOutStageResult(false, "Not a fan-out task (no AssigneeCompanyId)");

        var companyId = task.AssigneeCompanyId.Value;

        // 1b. Authorization — caller must belong to the task's company.
        // Internal admin bypasses the company gate (override path).
        // Note: we do NOT role-check task.AssignedTo here — that field is a group/pool LABEL
        // (e.g. "ExtAppraisalChecker"), not a role name, and after a stage transition with
        // AssignedType="1" it holds a username. The assignment pipeline already filtered the
        // candidate to the correct group when the task was assigned/reassigned.
        if (!IsInternalAdmin(currentUser))
        {
            if (!currentUser.CompanyId.HasValue || currentUser.CompanyId.Value != companyId)
                return new AdvanceFanOutStageResult(false,
                    "You may only act on tasks belonging to your own company.");
        }

        // 2. Load WorkflowInstance with activity executions
        var instance = await instanceRepository.GetWithExecutionsAsync(task.WorkflowInstanceId, cancellationToken);
        if (instance is null)
            return new AdvanceFanOutStageResult(false, "WorkflowInstance not found");

        // 3. Parse workflow JSON to get stage definitions for this activity
        var jsonDef = instance.WorkflowDefinition?.JsonDefinition;
        if (string.IsNullOrEmpty(jsonDef))
            return await FallbackToResumeAsync(command, task, companyId, cancellationToken);

        // Build a minimal ActivityContext so FanOutTaskActivity.ParseStageDefinitions works
        var activityProperties = ExtractActivityProperties(jsonDef, task.ActivityId);
        var activityCtx = new ActivityContext
        {
            WorkflowInstanceId = instance.Id,
            ActivityId = task.ActivityId,
            ActivityName = task.TaskName,
            Properties = activityProperties,
            Variables = instance.Variables,
            WorkflowInstance = instance
        };

        var stages = fanOutActivity.ParseStageDefinitions(activityCtx);
        if (stages.Count == 0)
            return await FallbackToResumeAsync(command, task, companyId, cancellationToken);

        // 4. Determine current stage from FanOutItems on the execution
        var execution = instance.ActivityExecutions
            .FirstOrDefault(e => e.ActivityId == task.ActivityId
                                 && e.Status == ActivityExecutionStatus.InProgress);

        var currentStageName = execution?.FanOutItems
            .FirstOrDefault(i => i.FanOutKey == companyId)?.CurrentStage
            ?? stages[0].Name; // fallback to first stage

        var currentStage = stages.FirstOrDefault(s => s.Name == currentStageName);
        if (currentStage is null)
            return new AdvanceFanOutStageResult(false, $"Unknown current stage '{currentStageName}'");

        // 5. Match the requested action
        var matchedAction = currentStage.Actions
            .FirstOrDefault(a => string.Equals(a.Value, command.ActionValue, StringComparison.OrdinalIgnoreCase));

        if (matchedAction is null)
        {
            // Idempotency-friendly: if the requested action would have transitioned us into the
            // CURRENT stage (the user already advanced past it on a previous, successful call),
            // treat as a no-op success rather than a hard error. Same for actions whose terminal
            // outcome matches the task's already-completed state — but here the task is still
            // pending so that case can't apply.
            var actionTargetsCurrentStage = stages.Any(s =>
                s.Actions.Any(a =>
                    string.Equals(a.Value, command.ActionValue, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a.To, currentStageName, StringComparison.OrdinalIgnoreCase)));

            if (actionTargetsCurrentStage)
            {
                logger.LogInformation(
                    "AdvanceFanOutStage: task {TaskId} company {CompanyId} action '{Action}' is a no-op (already in stage '{Stage}')",
                    task.Id, companyId, command.ActionValue, currentStageName);
                return new AdvanceFanOutStageResult(true, StageAdvanced: false, NextStage: currentStageName);
            }

            return new AdvanceFanOutStageResult(false,
                $"Action '{command.ActionValue}' not found in stage '{currentStageName}'");
        }

        // 6a. Stage-transition path
        if (!string.IsNullOrEmpty(matchedAction.To))
        {
            var nextStage = stages.FirstOrDefault(s => s.Name == matchedAction.To);
            if (nextStage is null)
                return new AdvanceFanOutStageResult(false,
                    $"Target stage '{matchedAction.To}' not found in activity definition");

            await fanOutActivity.AdvanceFanOutItemStageAsync(
                activityCtx, companyId, nextStage, command.CompletedBy, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "AdvanceFanOutStage: task {TaskId} company {CompanyId} advanced from {From} to {To}",
                task.Id, companyId, currentStageName, matchedAction.To);

            return new AdvanceFanOutStageResult(true, StageAdvanced: true, NextStage: matchedAction.To);
        }

        // 6b. Terminal path — delegate to existing ResumeWorkflow
        if (!string.IsNullOrEmpty(matchedAction.Complete))
        {
            var resumeInput = new Dictionary<string, object>
            {
                ["decisionTaken"] = matchedAction.Complete,
                ["completedBy"] = command.CompletedBy,
                ["companyId"] = companyId
            };

            if (command.AdditionalInput is not null)
                foreach (var kv in command.AdditionalInput)
                    resumeInput[kv.Key] = kv.Value;

            await workflowService.ResumeWorkflowAsync(
                instance.Id,
                task.ActivityId,
                command.CompletedBy,
                resumeInput,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "AdvanceFanOutStage: task {TaskId} company {CompanyId} completed with outcome {Outcome}",
                task.Id, companyId, matchedAction.Complete);

            return new AdvanceFanOutStageResult(true, StageAdvanced: false);
        }

        return new AdvanceFanOutStageResult(false, $"Action '{command.ActionValue}' has neither 'to' nor 'complete'");
    }

    private static bool IsInternalAdmin(ICurrentUserService user)
        => user.IsInRole("Admin") || user.IsInRole("IntAdmin");

    private Task<AdvanceFanOutStageResult> FallbackToResumeAsync(
        AdvanceFanOutStageCommand command,
        PendingTask task,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        // Legacy: no stages defined — caller should use the standard CompleteActivity endpoint.
        logger.LogWarning(
            "AdvanceFanOutStage: activity {ActivityId} has no stages — use CompleteActivity endpoint instead",
            task.ActivityId);
        return Task.FromResult(new AdvanceFanOutStageResult(false,
            "Activity has no stage definitions. Use POST /api/workflows/instances/{id}/activities/{activityId}/complete instead."));
    }

    private static Dictionary<string, object> ExtractActivityProperties(string jsonDefinition, string activityId)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonDefinition);
            var root = doc.RootElement;

            if (!root.TryGetProperty("activities", out var activities) ||
                activities.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, object>();

            foreach (var activity in activities.EnumerateArray())
            {
                if (!activity.TryGetProperty("id", out var idProp) ||
                    idProp.GetString() != activityId)
                    continue;

                if (!activity.TryGetProperty("properties", out var props))
                    return new Dictionary<string, object>();

                // Deserialize properties into a Dictionary<string, object> the same way
                // the engine does (JsonElement values, keyed by property name).
                var dict = new Dictionary<string, object>();
                foreach (var prop in props.EnumerateObject())
                    dict[prop.Name] = prop.Value.Clone();

                return dict;
            }
        }
        catch
        {
            // Fall through — return empty dict
        }

        return new Dictionary<string, object>();
    }
}
