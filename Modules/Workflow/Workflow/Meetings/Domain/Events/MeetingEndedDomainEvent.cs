using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingEndedDomainEvent(
    Guid MeetingId,
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId) : IDomainEvent;
