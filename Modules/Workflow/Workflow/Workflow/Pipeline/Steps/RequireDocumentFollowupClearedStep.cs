using Microsoft.Extensions.Logging;
using Workflow.Data.Entities;
using Workflow.Data.Repository;
using Workflow.DocumentFollowups.Application;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validation step extracted from the inline gate in CompleteActivityCommandHandler.
/// Blocks completion while the activity's associated pending task has open document followups.
/// Only applies when the activity has opted in via the 'canRaiseFollowup' property in the workflow definition.
/// </summary>
public class RequireDocumentFollowupClearedStep(
    IWorkflowInstanceRepository workflowInstanceRepository,
    IAssignmentRepository assignmentRepository,
    IDocumentFollowupGate documentFollowupGate,
    ILogger<RequireDocumentFollowupClearedStep> logger) : IActivityProcessStep
{
    public sealed record Parameters;

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "RequireDocumentFollowupCleared",
        displayName: "Require Document Followup Cleared",
        kind: StepKind.Validation,
        description: "Blocks completion while the task has open document followups. Only active when the activity opted in via 'canRaiseFollowup'.");

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        // Load the workflow instance (includes WorkflowDefinition)
        var workflowInstance =
            await workflowInstanceRepository.GetByIdAsync(ctx.WorkflowInstanceId, ct);

        if (workflowInstance is null)
        {
            logger.LogWarning(
                "Workflow instance {WorkflowInstanceId} not found in RequireDocumentFollowupClearedStep",
                ctx.WorkflowInstanceId);
            return ProcessStepResult.Pass(); // Can't block — instance missing
        }

        // Check if this activity opted in to the followup gate
        if (!ActivityFollowupHelpers.ActivityCanRaiseFollowup(workflowInstance, ctx.ActivityId))
        {
            logger.LogDebug(
                "Activity {ActivityId} has not opted in to document followup gate, skipping",
                ctx.ActivityId);
            return ProcessStepResult.Pass();
        }

        // Resolve the canonical task name and look up the pending task
        var correlationGuid = !string.IsNullOrEmpty(workflowInstance.CorrelationId) &&
                              Guid.TryParse(workflowInstance.CorrelationId, out var parsed)
            ? parsed
            : workflowInstance.Id;

        var taskName = ActivityFollowupHelpers.ResolveActivityName(workflowInstance, ctx.ActivityId)
                       ?? ctx.ActivityId;

        var pendingTask = await assignmentRepository.GetPendingTaskAsync(
            correlationGuid, taskName, ct);

        if (pendingTask is null)
        {
            // No pending task found — gate doesn't apply
            return ProcessStepResult.Pass();
        }

        var hasOpen = await documentFollowupGate.HasOpenFollowupAsync(pendingTask.Id, ct);
        if (hasOpen)
        {
            logger.LogWarning(
                "Activity {ActivityId} on workflow {WorkflowInstanceId} has open document followups",
                ctx.ActivityId, ctx.WorkflowInstanceId);

            return ProcessStepResult.Fail(
                "OPEN_DOCUMENT_FOLLOWUPS",
                "This task has open document followups. Resolve or cancel them before submitting.");
        }

        return ProcessStepResult.Pass();
    }
}
