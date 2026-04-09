using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record AppraisalAwaitingMeetingEvent(
    Guid AppraisalId,
    string? AppraisalNo,
    decimal FacilityLimit,
    Guid WorkflowInstanceId,
    string ActivityId) : IDomainEvent;
