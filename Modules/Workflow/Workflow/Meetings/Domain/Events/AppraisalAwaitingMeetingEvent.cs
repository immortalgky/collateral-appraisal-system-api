using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record AppraisalAwaitingMeetingEvent(
    Guid AppraisalId,
    string? AppraisalNo,
    decimal FacilityLimit,
    decimal AppraisalValue,
    Guid WorkflowInstanceId,
    string ActivityId) : IDomainEvent;
