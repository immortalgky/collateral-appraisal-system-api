using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingCancelledDomainEvent(
    Guid MeetingId,
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId,
    string? Reason) : IDomainEvent;
