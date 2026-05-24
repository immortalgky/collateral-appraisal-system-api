using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Handles end-of-meeting housekeeping (per-item secretary flow):
/// - Marks all Assigned <see cref="MeetingQueueItem"/> rows for this meeting as Released.
/// - Marks all Included <see cref="AppraisalAcknowledgementQueueItem"/> rows as Acknowledged and
///   notifies the Appraisal module so it can link each acknowledged appraisal's review row to this meeting.
/// </summary>
public class MeetingEndedDomainEventHandler(
    WorkflowDbContext dbContext,
    IWorkflowUnitOfWork unitOfWork,
    IIntegrationEventOutbox outbox,
    ILogger<MeetingEndedDomainEventHandler> logger)
    : INotificationHandler<MeetingEndedDomainEvent>
{
    public async Task Handle(MeetingEndedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meeting {MeetingId} ended at {EndedAt}",
            notification.MeetingId, notification.EndedAt);

        // ----- Release all Assigned queue items for this meeting -----
        var queueItems = await dbContext.MeetingQueueItems
            .Where(q => q.MeetingId == notification.MeetingId && q.Status == MeetingQueueItemStatus.Assigned)
            .ToListAsync(cancellationToken);

        foreach (var qi in queueItems)
            qi.Release();

        logger.LogInformation(
            "Meeting {MeetingId}: released {Count} MeetingQueueItem(s)",
            notification.MeetingId, queueItems.Count);

        // ----- Acknowledge all Included ack items for this meeting -----
        var ackItems = await dbContext.AppraisalAcknowledgementQueueItems
            .Where(a => a.MeetingId == notification.MeetingId && a.Status == AcknowledgementStatus.Included)
            .ToListAsync(cancellationToken);

        foreach (var ai in ackItems)
            ai.Acknowledge();

        logger.LogInformation(
            "Meeting {MeetingId}: acknowledged {Count} AppraisalAcknowledgementQueueItem(s)",
            notification.MeetingId, ackItems.Count);

        // Notify the Appraisal module so it can link each acknowledged appraisal's review row to
        // this meeting. Outbox publish is atomic with the SaveChanges below.
        foreach (var ai in ackItems)
        {
            outbox.Publish(new AppraisalAcknowledgedIntegrationEvent
            {
                AppraisalId = ai.AppraisalId,
                MeetingId = notification.MeetingId
            }, ai.AppraisalId.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
