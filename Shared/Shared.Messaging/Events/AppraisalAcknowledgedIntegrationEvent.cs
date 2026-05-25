namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow Meetings module when a sub-committee approval that was queued
/// for acknowledgement is read into a meeting and that meeting ends (the
/// <c>AppraisalAcknowledgementQueueItem</c> transitions to Acknowledged).
/// Consumer:
/// - Appraisal module: links the appraisal's Committee <c>AppraisalReview</c> row to the
///   acknowledgement meeting.
/// </summary>
public record AppraisalAcknowledgedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid MeetingId { get; init; }
}
