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
    public Task Consume(ConsumeContext<WorkflowCancelled> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<WorkflowCancelled> context)
    {
        var msg = context.Message;

        // Resolve the appraisalId from the cancelled workflow's variables
        var workflowInstance = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == msg.WorkflowInstanceId, context.CancellationToken);

        if (workflowInstance is null)
        {
            return;
        }

        // Read appraisalId from variables (same helper pattern as RaiseHandler)
        if (!workflowInstance.Variables.TryGetValue("appraisalId", out var rawAppraisalId))
        {
            return;
        }

        if (!Guid.TryParse(rawAppraisalId?.ToString(), out var appraisalId))
        {
            return;
        }

        var openApprovals = await dbContext.FeeAppointmentApprovals
            .Where(a => a.AppraisalId == appraisalId && a.Status == FeeAppointmentApprovalStatus.Open)
            .ToListAsync(context.CancellationToken);

        if (openApprovals.Count == 0)
        {
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
    }
}
