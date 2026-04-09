namespace Workflow.DocumentFollowups.Domain.Events;

public record DocumentFollowupRaisedDomainEvent(Guid FollowupId, Guid RaisingPendingTaskId) : IDomainEvent;

public record DocumentFollowupResolvedDomainEvent(Guid FollowupId, Guid RaisingPendingTaskId) : IDomainEvent;

public record DocumentFollowupCancelledDomainEvent(Guid FollowupId, Guid RaisingPendingTaskId, string Reason) : IDomainEvent;
