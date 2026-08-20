using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

// Co-located on the partitioned "appraisal-sync" endpoint (Program.cs)
// with the company/internal-followup handlers so all assignment events for one appraisal are serialized.
[ExcludeFromConfigureEndpoints]
public class InternalAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    IAssignmentFeeService feeService,
    ILogger<InternalAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard
) : IConsumer<InternalAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InternalAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, AssigneeUserId: {AssigneeUserId}",
            nameof(InternalAssignedIntegrationEvent), message.AppraisalId, message.AssigneeUserId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for internal assignment", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Find the current active assignment (latest non-cancelled, non-rejected)
        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Capture before Assign() (which unconditionally sets Assigned) so we can tell an initial
        // assignment apart from a routeback re-fire of this event. Pending/Assigned/InProgress are all
        // the initial-assignment lifecycle; UnderReview/Verified/Completed mean a routeback re-fire.
        var isInitialAssignment = assignment.AssignmentStatus == AssignmentStatus.Pending
            || assignment.AssignmentStatus == AssignmentStatus.Assigned
            || assignment.AssignmentStatus == AssignmentStatus.InProgress;

        // The assignee is internal on both paths this event covers, but the CASE is not: an
        // off-system engagement stays External so reporting, the AS400/LOS feed and fee resolution
        // keep treating it as external work. Hardcoding "Internal" here would silently flip those
        // appraisals. Defaults to "Internal" on the event, so existing publishers are unchanged.
        //
        // Assign() defaults every optional parameter to null, so anything not passed back is WIPED.
        // This event now re-fires on every landing at int-offline-book-keyin, including a route-back
        // from int-appraisal-check — and unlike the internal path, an off-system case carries state
        // this event does not know about:
        //   AssigneeCompanyId  — the external company the keyer recorded via
        //                        SetOfflineExternalEngagement. Losing it blanks the company on the
        //                        printed book and the AS400/LOS feed, breaks fee/company linkage,
        //                        and flips ExternalCompanyRecorded back to 0 so the keyer is blocked.
        //   ReassignmentNumber — the rework count; resetting it to 1 discards the routeback history.
        // Both are read off the row and passed straight back. The internal path is unaffected: an
        // INT case has no company, and its ReassignmentNumber round-trips unchanged.
        assignment.Assign(
            assignmentType: message.AssignmentType,
            assigneeUserId: message.AssigneeUserId,
            assigneeCompanyId: assignment.AssigneeCompanyId,
            internalAppraiserId: message.InternalAppraiserId,
            assignmentMethod: message.AssignmentMethod,
            internalFollowupMethod: message.InternalFollowupAssignmentMethod,
            reassignmentNumber: assignment.ReassignmentNumber,
            assignedBy: "System");

        // Internal path: the assignee IS the int-appraisal-execution executor, and this event is
        // published only while the workflow is already on int-appraisal-execution, so "assigned" and
        // "work started" are the same moment. Advance to InProgress here instead of relying on the
        // WorkflowTransitioned StartWork — that runs on a separate endpoint and, arriving before this
        // Assign, would see Pending and silently drop the transition (leaving it stuck at Assigned).
        // Guarded on isInitialAssignment so a routeback re-fire (UnderReview/Verified) is NOT reset —
        // routeback stays owned by the status handler's Rework() path. Treating an already-Assigned/
        // InProgress assignment as initial keeps a message redelivery idempotent: Assign() reset the
        // status to Assigned above, so StartWork() re-advances to InProgress instead of leaving the
        // assignment regressed at Assigned (the MarkAsProcessed inbox-claim commit is separate from
        // this business write, so a crash in that window can replay this handler).
        if (isInitialAssignment)
            assignment.StartWork();

        var feeSource = await feeService.ResolveSourceForAppraisalAsync(
            appraisal, new AssignmentFeeSource.TierBased(), ct);

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: message.AppraisalId,
            assignmentId: assignment.Id,
            source: feeSource,
            ct: ct);

        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Updated AppraisalAssignment for AppraisalId {AppraisalId}: Internal, AssigneeUserId={UserId}, Method={Method}",
            message.AppraisalId, message.AssigneeUserId, message.AssignmentMethod);
    }
}
