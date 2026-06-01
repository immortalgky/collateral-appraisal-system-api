using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.FeeAppointmentApprovals.Domain;
using Workflow.FeeAppointmentApprovals.Domain.Events;

namespace Workflow.FeeAppointmentApprovals.EventHandlers;

/// <summary>
/// Translates domain events raised by FeeAppointmentApproval into notification integration events
/// via the persistent outbox, so they commit atomically with the aggregate write.
/// </summary>
public class FeeAppointmentApprovalRaisedNotificationHandler(
    IIntegrationEventOutbox outbox,
    ILogger<FeeAppointmentApprovalRaisedNotificationHandler> logger)
    : INotificationHandler<FeeAppointmentApprovalRaisedDomainEvent>
{
    public Task Handle(FeeAppointmentApprovalRaisedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Notify the approver that a new approval task is waiting
        outbox.Publish(new FeeAppointmentApprovalNotificationIntegrationEvent
        {
            Type = "FeeAppointmentApprovalRaised",
            ApprovalId = notification.ApprovalId,
            AppraisalId = notification.AppraisalId,
            Recipient = notification.ApproverAssignee,
            Title = "Fee & Appointment Approval Required",
            Message = "An external company has submitted changes that require your approval."
        }, correlationId: notification.AppraisalId.ToString());

        logger.LogInformation(
            "Published FeeAppointmentApprovalRaised notification for approval {ApprovalId}, approver {Approver}",
            notification.ApprovalId, notification.ApproverAssignee);

        return Task.CompletedTask;
    }
}

public class FeeAppointmentApprovalResolvedNotificationHandler(
    WorkflowDbContext dbContext,
    IIntegrationEventOutbox outbox,
    ILogger<FeeAppointmentApprovalResolvedNotificationHandler> logger)
    : INotificationHandler<FeeAppointmentApprovalResolvedDomainEvent>
{
    public async Task Handle(FeeAppointmentApprovalResolvedDomainEvent notification, CancellationToken cancellationToken)
    {
        var approval = await dbContext.FeeAppointmentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == notification.ApprovalId, cancellationToken);

        if (approval is null)
        {
            logger.LogWarning("FeeAppointmentApprovalResolvedNotificationHandler: approval {Id} not found", notification.ApprovalId);
            return;
        }

        outbox.Publish(new FeeAppointmentApprovalResolvedIntegrationEvent
        {
            AppraisalId = notification.AppraisalId,
            ApprovalId = notification.ApprovalId,
            LineOutcomes = notification.LineOutcomes
                .Select(o => new FeeApprovalLineOutcome(
                    o.LineType, o.TargetId, o.Decision, o.Reason))
                .ToList()
        }, correlationId: notification.AppraisalId.ToString());

        logger.LogInformation(
            "Published FeeAppointmentApprovalResolved integration event for approval {ApprovalId}",
            notification.ApprovalId);
    }
}

public class FeeAppointmentApprovalCancelledNotificationHandler(
    IIntegrationEventOutbox outbox,
    ILogger<FeeAppointmentApprovalCancelledNotificationHandler> logger)
    : INotificationHandler<FeeAppointmentApprovalCancelledDomainEvent>
{
    public Task Handle(FeeAppointmentApprovalCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        outbox.Publish(new FeeAppointmentApprovalNotificationIntegrationEvent
        {
            Type = "FeeAppointmentApprovalCancelled",
            ApprovalId = notification.ApprovalId,
            AppraisalId = notification.AppraisalId,
            Title = "Fee & Appointment Approval Cancelled",
            Message = $"Approval was cancelled: {notification.Reason}",
            Reason = notification.Reason
        }, correlationId: notification.AppraisalId.ToString());

        logger.LogInformation(
            "Published FeeAppointmentApprovalCancelled notification for approval {ApprovalId}",
            notification.ApprovalId);

        return Task.CompletedTask;
    }
}
