using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

public class AwaitSignalActivity(
    WorkflowDbContext dbContext,
    ILogger<AwaitSignalActivity> logger) : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.AwaitSignalActivity;
    public override string Name => "Await Signal Activity";
    public override string Description => "Suspends workflow until a named signal arrives";

    protected override Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var signalName = GetProperty<string>(context, "signalName");
        var correlationKey = GetProperty<string>(context, "correlationKey");
        var completionVariable = GetProperty<string>(context, "completionVariable", "");

        if (string.IsNullOrEmpty(signalName) || string.IsNullOrEmpty(correlationKey))
            return Task.FromResult(
                ActivityResult.Failed("AwaitSignalActivity requires 'signalName' and 'correlationKey' properties"));

        var correlationValue = GetVariable<string>(context, correlationKey, "");
        if (string.IsNullOrEmpty(correlationValue))
            return Task.FromResult(
                ActivityResult.Failed(
                    $"Correlation variable '{correlationKey}' is missing or empty in workflow variables"));

        // Early-exit guard: if the completion variable is already set, skip the suspend
        if (!string.IsNullOrEmpty(completionVariable))
        {
            var existingValue = GetVariable<string>(context, completionVariable, "");
            if (!string.IsNullOrEmpty(existingValue))
            {
                logger.LogInformation(
                    "AwaitSignalActivity {ActivityId}: completionVariable '{CompletionVariable}' already set, skipping suspend",
                    context.ActivityId, completionVariable);

                return Task.FromResult(ActivityResult.Success(new Dictionary<string, object>
                {
                    ["signalSkipped"] = true,
                    ["signalName"] = signalName
                }));
            }
        }

        var bookmark = WorkflowBookmark.Create(
            context.WorkflowInstanceId,
            context.ActivityId,
            BookmarkType.ExternalMessage,
            key: signalName,
            correlationId: correlationValue);

        // Add to change tracker only — do NOT call SaveChangesAsync.
        // The engine's outer transaction flushes this atomically with the
        // outbox message and workflow state update.
        dbContext.WorkflowBookmarks.Add(bookmark);

        logger.LogInformation(
            "AwaitSignalActivity {ActivityId}: suspended workflow {WorkflowInstanceId} waiting for signal '{SignalName}' with correlation '{CorrelationValue}'",
            context.ActivityId, context.WorkflowInstanceId, signalName, correlationValue);

        return Task.FromResult(ActivityResult.Pending(new Dictionary<string, object>
        {
            ["signalName"] = signalName,
            ["correlationKey"] = correlationKey,
            ["correlationValue"] = correlationValue,
            ["subscribedAt"] = DateTime.UtcNow
        }));
    }

    protected override Task<ActivityResult> ResumeActivityAsync(
        ActivityContext context,
        Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default)
    {
        var signalName = GetProperty<string>(context, "signalName", "");

        logger.LogInformation(
            "AwaitSignalActivity {ActivityId}: resumed by signal '{SignalName}' for workflow {WorkflowInstanceId}",
            context.ActivityId, signalName, context.WorkflowInstanceId);

        return Task.FromResult(ActivityResult.Success(resumeInput));
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            "SYSTEM",
            context.Variables);
    }
}
