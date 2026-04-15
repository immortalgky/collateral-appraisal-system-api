using Workflow.Meetings.Domain.Events;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Placeholder handler for <see cref="MeetingInvitationSentDomainEvent"/>.
/// Currently logs a structured event; future versions will dispatch email notifications.
/// </summary>
public class MeetingInvitationSentDomainEventHandler(
    ILogger<MeetingInvitationSentDomainEventHandler> logger)
    : INotificationHandler<MeetingInvitationSentDomainEvent>
{
    public Task Handle(MeetingInvitationSentDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meeting invitation sent: MeetingId={MeetingId}, MeetingNo={MeetingNo}",
            notification.MeetingId, notification.MeetingNo);

        // TODO: integrate with Notification module to dispatch invitation emails to meeting members.
        return Task.CompletedTask;
    }
}
