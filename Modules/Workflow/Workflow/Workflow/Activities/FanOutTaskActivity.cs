using System.Text.Json;
using Workflow.Data.Repository;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;
using MassTransit;
using Shared.Messaging.Events;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Creates N PendingTasks (one per invited company) when executed.
/// Transitions to the next activity only when ALL per-company tasks are terminal.
///
/// JSON activity definition properties:
///   fanOutVariable      : name of the workflow variable holding Guid[] of company IDs (e.g. "invitedCompanyIds")
///   assigneeGroup       : role group for each task (e.g. "ExtAdmin")
///   activityName        : display name stored on each PendingTask row
///   dueAtVariable       : (optional) workflow variable name holding the DateTime? due date
/// </summary>
public class FanOutTaskActivity(
    IAssignmentRepository assignmentRepository,
    IPublisher publisher,
    IPublishEndpoint publishEndpoint,
    IDateTimeProvider dateTimeProvider,
    ILogger<FanOutTaskActivity> logger)
    : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.FanOutTaskActivity;
    public override string Name => "Fan-Out Task Activity";
    public override string Description => "Spawns one task per company and gates transition until all are terminal";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var fanOutVariable = GetProperty(context, "fanOutVariable", "invitedCompanyIds");
            var activityName = GetProperty(context, "activityName", context.ActivityId);
            var assigneeGroup = GetProperty(context, "assigneeGroup", "ExtAdmin");

            // Resolve company IDs from workflow variables
            var companyIds = ResolveCompanyIds(context.Variables, fanOutVariable);

            if (companyIds.Count == 0)
            {
                logger.LogWarning(
                    "FanOutTaskActivity {ActivityId}: no company IDs found in variable '{FanOutVariable}'",
                    context.ActivityId, fanOutVariable);
                return ActivityResult.Failed($"Fan-out variable '{fanOutVariable}' is empty or missing");
            }

            // Resolve optional DueAt
            DateTime? dueAt = null;
            var dueAtVariable = GetProperty<string>(context, "dueAtVariable");
            if (!string.IsNullOrEmpty(dueAtVariable) &&
                context.Variables.TryGetValue(dueAtVariable, out var dueAtRaw))
                dueAt = ResolveDateTime(dueAtRaw);

            var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
                                  && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                ? parsed
                : context.WorkflowInstance.Id;

            var assignments = companyIds
                .Select(companyId => new FanOutCompanyAssignment(
                    companyId,
                    assigneeGroup, // For pool tasks, AssignedTo = the group/role
                    activityName))
                .ToList();

            // Publish fan-out event — handler archives previous task and creates N rows
            await publisher.Publish(
                new FanOutTasksAssignedEvent(
                    correlationGuid,
                    activityName,
                    assignments,
                    dateTimeProvider.ApplicationNow,
                    context.WorkflowInstanceId,
                    context.ActivityId,
                    dueAt,
                    context.WorkflowInstance.StartedBy,
                    context.WorkflowInstance.Name),
                cancellationToken);

            logger.LogInformation(
                "FanOutTaskActivity {ActivityId}: spawned {Count} tasks for companies [{CompanyIds}]",
                context.ActivityId, companyIds.Count, string.Join(", ", companyIds));

            return ActivityResult.Pending(new Dictionary<string, object>
            {
                ["fanOutCount"] = companyIds.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FanOutTaskActivity {ActivityId}: execution failed", context.ActivityId);
            return ActivityResult.Failed($"FanOutTaskActivity failed: {ex.Message}");
        }
    }

    protected override async Task<ActivityResult> ResumeActivityAsync(
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract companyId from the resume input (set by the command handler)
            if (!resumeInput.TryGetValue("companyId", out var companyIdRaw) ||
                !Guid.TryParse(companyIdRaw?.ToString(), out var companyId))
            {
                logger.LogWarning(
                    "FanOutTaskActivity {ActivityId}: resume input missing companyId",
                    context.ActivityId);
                // Fall through — treat as non-company-scoped completion (admin/system)
            }
            else
            {
                // Archive this specific company's PendingTask
                var companyTask = await assignmentRepository.GetFanOutTaskByCompanyAsync(
                    context.WorkflowInstanceId, context.ActivityId, companyId, cancellationToken);

                if (companyTask is not null)
                {
                    var actionTaken = resumeInput.TryGetValue("decisionTaken", out var dt)
                        ? dt?.ToString() ?? "Completed"
                        : "Completed";
                    var completedBy = resumeInput.TryGetValue("completedBy", out var cb)
                        ? cb?.ToString()
                        : context.WorkflowInstance.CurrentAssignee;

                    var completedTask = CompletedTask.CreateFromPendingTask(
                        companyTask, actionTaken, dateTimeProvider.ApplicationNow);
                    await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
                    await assignmentRepository.RemovePendingTaskAsync(companyTask, cancellationToken);

                    // Publish integration event for notifications
                    await publishEndpoint.Publish(new TaskCompletedIntegrationEvent
                    {
                        CorrelationId = companyTask.CorrelationId,
                        TaskName = companyTask.TaskName,
                        ActionTaken = actionTaken,
                        CompletedBy = completedBy ?? companyTask.AssignedTo,
                        WorkflowInstanceName = context.WorkflowInstance.Name,
                        Movement = "F"
                    }, cancellationToken);

                    logger.LogInformation(
                        "FanOutTaskActivity {ActivityId}: completed task for company {CompanyId}",
                        context.ActivityId, companyId);
                }
            }

            // Check if all fan-out tasks for this activity are now terminal (no PendingTasks left)
            var remaining = await assignmentRepository.GetFanOutPendingTasksAsync(
                context.WorkflowInstanceId, context.ActivityId, cancellationToken);

            if (remaining.Count > 0)
            {
                logger.LogInformation(
                    "FanOutTaskActivity {ActivityId}: {Remaining} tasks still pending — staying paused",
                    context.ActivityId, remaining.Count);

                // Still waiting — return Pending so engine does not transition
                return ActivityResult.Pending(new Dictionary<string, object>
                {
                    ["remainingCount"] = remaining.Count
                });
            }

            // All done — build output data and transition
            var decisionTaken = resumeInput.TryGetValue("decisionTaken", out var finalDt)
                ? finalDt?.ToString() ?? "Completed"
                : "Completed";

            var outputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"] = decisionTaken,
                ["decision"] = "all_completed"
            };

            logger.LogInformation(
                "FanOutTaskActivity {ActivityId}: all tasks completed — transitioning",
                context.ActivityId);

            return ActivityResult.Success(outputData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FanOutTaskActivity {ActivityId}: resume failed", context.ActivityId);
            return ActivityResult.Failed($"FanOutTaskActivity resume failed: {ex.Message}");
        }
    }

    public override Task<Core.ValidationResult> ValidateAsync(
        ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var fanOutVariable = GetProperty<string>(context, "fanOutVariable");
        if (string.IsNullOrEmpty(fanOutVariable))
            errors.Add("FanOutTaskActivity: 'fanOutVariable' property is required");

        return Task.FromResult(errors.Any()
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            context.CurrentAssignee,
            context.Variables);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Guid> ResolveCompanyIds(Dictionary<string, object> variables, string variableName)
    {
        if (!variables.TryGetValue(variableName, out var raw) || raw is null)
            return [];

        // Handle JsonElement array (most common after EF JSON round-trip)
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String
                    ? Guid.TryParse(e.GetString(), out var g) ? g : (Guid?)null
                    : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

        // Handle List<object> or List<Guid>
        if (raw is IEnumerable<object> list)
            return list
                .Select(item => item switch
                {
                    Guid g => g,
                    string s when Guid.TryParse(s, out var g) => g,
                    JsonElement e when Guid.TryParse(e.GetString(), out var g) => g,
                    _ => (Guid?)null
                })
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

        // Handle Guid[]
        if (raw is Guid[] ids) return ids.ToList();

        return [];
    }

    private static DateTime? ResolveDateTime(object? raw)
    {
        return raw switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, out var d) => d,
            JsonElement je when je.ValueKind == JsonValueKind.String
                                && DateTime.TryParse(je.GetString(), out var d) => d,
            _ => null
        };
    }
}