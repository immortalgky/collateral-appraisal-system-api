using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingCutOffDomainEvent(
    Guid MeetingId,
    IReadOnlyList<Guid> IncludedAppraisalIds) : IDomainEvent;
