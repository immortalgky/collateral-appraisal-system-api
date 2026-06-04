using Shared.Messaging.Filters;
using Workflow.FeeAppointmentApprovals.Domain;
using Workflow.Workflow.Events;
using Workflow.Workflow.Services;

namespace Workflow.FeeAppointmentApprovals.EventHandlers;

/// <summary>
/// Cascades cancellation: when a parent appraisal workflow is cancelled, all open
/// FeeAppointmentApprovals for that workflow's appraisal are auto-cancelled and their
/// child workflows are also cancelled.
/// Mirrors DocumentFollowups.EventHandlers.ParentWorkflowCancelledConsumer.
/// </summary>
public class FeeApprovalParentWorkflowCancelledConsumer(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    IPublisher publisher,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<FeeApprovalParentWorkflowCancelledConsumer> logger)
    : IConsumer<WorkflowCancelled>
{
    public async Task Consume(ConsumeContext<WorkflowCancelled> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;

        // Resolve the appraisalId from the cancelled workflow's variables
        var workflowInstance = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == msg.WorkflowInstanceId, context.CancellationToken);

        if (workflowInstance is null)
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        // Read appraisalId from variables (same helper pattern as RaiseHandler)
        if (!workflowInstance.Variables.TryGetValue("appraisalId", out var rawAppraisalId))
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        if (!Guid.TryParse(rawAppraisalId?.ToString(), out var appraisalId))
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        var openApprovals = await dbContext.FeeAppointmentApprovals
            .Where(a => a.AppraisalId == appraisalId && a.Status == FeeAppointmentApprovalStatus.Open)
            .ToListAsync(context.CancellationToken);

        if (openApprovals.Count == 0)
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        const string systemReason = "Parent appraisal workflow cancelled";

        foreach (var approval in openApprovals)
        {
            try
            {
                approval.Cancel(systemReason);
                await dbContext.SaveChangesAsync(context.CancellationToken);

                foreach (var ev in approval.ClearDomainEvents())
                    await publisher.Publish(ev, context.CancellationToken);

                if (approval.FollowupWorkflowInstanceId.HasValue)
                {
                    await workflowService.CancelWorkflowAsync(
                        approval.FollowupWorkflowInstanceId.Value,
                        "system",
                        systemReason,
                        context.CancellationToken);
                }

                logger.LogInformation(
                    "Cascade-cancelled FeeAppointmentApproval {ApprovalId} for appraisal {AppraisalId}",
                    approval.Id, appraisalId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to cascade-cancel FeeAppointmentApproval {ApprovalId} for appraisal {AppraisalId}",
                    approval.Id, appraisalId);
            }
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
