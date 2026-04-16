using Microsoft.Extensions.Options;
using Workflow.Meetings.Configuration;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Services;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Handles end-of-meeting housekeeping:
/// - Marks all Assigned <see cref="MeetingQueueItem"/> rows for this meeting as Released.
/// - Marks all Included <see cref="AppraisalAcknowledgementQueueItem"/> rows for this meeting as Acknowledged.
///
/// Feature flag <c>Workflow:SecretaryPerItemEnabled</c> (default true):
///   true  → new per-item secretary flow; no workflow resume here.
///   false → legacy: resume all decision items immediately on End (rollback path).
/// </summary>
public class MeetingEndedDomainEventHandler(
    WorkflowDbContext dbContext,
    IWorkflowUnitOfWork unitOfWork,
    IWorkflowService workflowService,
    IConfiguration configuration,
    ILogger<MeetingEndedDomainEventHandler> logger)
    : INotificationHandler<MeetingEndedDomainEvent>
{
    public async Task Handle(MeetingEndedDomainEvent notification, CancellationToken cancellationToken)
    {
        var secretaryPerItemEnabled = configuration.GetValue<bool>("Workflow:SecretaryPerItemEnabled", defaultValue: true);

        logger.LogInformation(
            "Meeting {MeetingId} ended at {EndedAt}. SecretaryPerItemEnabled={SecretaryPerItemEnabled}",
            notification.MeetingId, notification.EndedAt, secretaryPerItemEnabled);

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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // ----- Legacy path: resume all decision items immediately (rollback feature flag) -----
        if (!secretaryPerItemEnabled)
        {
            logger.LogWarning(
                "Meeting {MeetingId}: SecretaryPerItemEnabled=false — using legacy resume-on-end. " +
                "This path is a rollback safety net; enable per-item flow when in-flight meetings are cleared.",
                notification.MeetingId);

            // Only Decision items have a workflow to resume; Acknowledgement items have null WorkflowInstanceId/ActivityId.
            var meetingItems = await dbContext.MeetingItems
                .Where(i => i.MeetingId == notification.MeetingId
                            && i.WorkflowInstanceId != null
                            && i.ActivityId != null)
                .ToListAsync(cancellationToken);

            foreach (var item in meetingItems)
            {
                try
                {
                    await workflowService.ResumeWorkflowAsync(
                        workflowInstanceId: item.WorkflowInstanceId!.Value,
                        activityId: item.ActivityId!,
                        completedBy: "system",
                        input: new Dictionary<string, object>
                        {
                            ["meetingId"] = notification.MeetingId,
                            ["meetingOutcome"] = MeetingOutcomes.Released,
                            ["completedBy"] = "system"
                        },
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Meeting {MeetingId}: failed to resume workflow {WorkflowInstanceId} for appraisal {AppraisalId} (legacy path)",
                        notification.MeetingId, item.WorkflowInstanceId, item.AppraisalId);
                }
            }
        }
    }
}
