using Shared.DDD;

namespace Workflow.Meetings.Domain.Events;

public record MeetingItemRoutedBackDomainEvent(
    Guid MeetingId,
    Guid AppraisalId,
    Guid WorkflowInstanceId,
    string ActivityId,
    string Reason,
    string RoutedBackBy) : IDomainEvent;
