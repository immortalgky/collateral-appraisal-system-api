using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class InternalAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<InternalAssignedIntegrationEventHandler> logger
) : IConsumer<InternalAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InternalAssignedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, AssigneeUserId: {AssigneeUserId}",
            nameof(InternalAssignedIntegrationEvent), message.AppraisalId, message.AssigneeUserId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(
            message.AppraisalId, context.CancellationToken);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for internal assignment", message.AppraisalId);
            return;
        }

        // Find the current active assignment (latest non-cancelled, non-rejected)
        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            return;
        }

        assignment.Assign(
            assignmentType: "Internal",
            assigneeUserId: message.AssigneeUserId,
            internalAppraiserId: message.InternalAppraiserId,
            assignmentMethod: message.AssignmentMethod,
            internalFollowupMethod: message.InternalFollowupAssignmentMethod,
            assignedBy: "System");

        await appraisalRepository.UpdateAsync(appraisal, context.CancellationToken);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Updated AppraisalAssignment for AppraisalId {AppraisalId}: Internal, AssigneeUserId={UserId}, Method={Method}",
            message.AppraisalId, message.AssigneeUserId, message.AssignmentMethod);
    }
}
