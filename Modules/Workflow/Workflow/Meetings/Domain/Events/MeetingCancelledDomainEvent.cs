using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

/// <summary>
/// Raised exactly once when a meeting is cancelled, regardless of how many decision items it has.
/// <see cref="DecisionItems"/> may be empty for meetings with zero decision items.
/// </summary>
public record MeetingCancelledDomainEvent(
    Guid MeetingId,
    string? Reason,
    DateTime CancelledAt,
    IReadOnlyList<CancelledDecisionItem> DecisionItems) : IDomainEvent;

/// <summary>
/// Identifies a decision item that was pending when the meeting was cancelled.
/// </summary>
public record CancelledDecisionItem(
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId);
