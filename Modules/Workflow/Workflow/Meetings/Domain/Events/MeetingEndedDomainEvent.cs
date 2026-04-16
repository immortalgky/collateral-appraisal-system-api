using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingEndedDomainEvent(
    Guid MeetingId,
    DateTime EndedAt) : IDomainEvent;
