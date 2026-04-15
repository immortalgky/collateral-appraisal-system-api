using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingItemReleasedDomainEvent(
    Guid MeetingId,
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId,
    string ReleasedBy,
    IReadOnlyList<string> MemberUserIds) : IDomainEvent;
