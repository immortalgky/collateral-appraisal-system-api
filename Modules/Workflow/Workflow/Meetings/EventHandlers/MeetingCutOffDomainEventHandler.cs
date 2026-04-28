using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// When a meeting is cut off, flips each <see cref="AppraisalAcknowledgementQueueItem"/>
/// whose <see cref="AppraisalAcknowledgementQueueItem.AppraisalId"/> appears in
/// <see cref="MeetingCutOffDomainEvent.IncludedAppraisalIds"/> and whose status is
/// <see cref="AcknowledgementStatus.PendingAcknowledgement"/> to
/// <see cref="AcknowledgementStatus.Included"/>.
///
/// Rationale: <see cref="Domain.Meeting.CutOff"/> calls <c>ai.Include(meetingId)</c> on items
/// already loaded in-process, but ack items loaded separately (e.g. in a different query path)
/// need to be updated here as a bridge between the domain and the read-model rows.
/// </summary>
public class MeetingCutOffDomainEventHandler(
    WorkflowDbContext dbContext,
    IWorkflowUnitOfWork unitOfWork,
    ILogger<MeetingCutOffDomainEventHandler> logger)
    : INotificationHandler<MeetingCutOffDomainEvent>
{
    public async Task Handle(MeetingCutOffDomainEvent notification, CancellationToken cancellationToken)
    {
        // if (notification.IncludedAppraisalIds.Count == 0)
        //     return;
        //
        // var ackItems = await dbContext.AppraisalAcknowledgementQueueItems
        //     .Where(a =>
        //         notification.IncludedAppraisalIds.Contains(a.AppraisalId) &&
        //         a.Status == AcknowledgementStatus.PendingAcknowledgement)
        //     .ToListAsync(cancellationToken);
        //
        // foreach (var ai in ackItems)
        // {
        //     ai.Include(notification.MeetingId);
        // }
        //
        // if (ackItems.Count > 0)
        // {
        //     logger.LogInformation(
        //         "Meeting {MeetingId} cut-off: included {Count} AppraisalAcknowledgementQueueItem(s)",
        //         notification.MeetingId, ackItems.Count);
        //
        //     await unitOfWork.SaveChangesAsync(cancellationToken);
        // }
    }
}