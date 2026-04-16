using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingInvitationSentDomainEvent(
    Guid MeetingId,
    string MeetingNo) : IDomainEvent;
