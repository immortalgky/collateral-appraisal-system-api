using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

public class InternalFollowupAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<InternalFollowupAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard
) : IConsumer<InternalFollowupAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InternalFollowupAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, InternalAppraiserId: {InternalAppraiserId}",
            nameof(InternalFollowupAssignedIntegrationEvent), message.AppraisalId, message.InternalAppraiserId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for internal followup assignment", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId} when attaching internal followup",
                message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        assignment.AssignInternalFollowup(
            message.InternalAppraiserId,
            message.InternalFollowupAssignmentMethod);

        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Attached internal followup to AppraisalAssignment for AppraisalId {AppraisalId}: StaffId={StaffId}, Method={Method}",
            message.AppraisalId, message.InternalAppraiserId, message.InternalFollowupAssignmentMethod);
    }
}
