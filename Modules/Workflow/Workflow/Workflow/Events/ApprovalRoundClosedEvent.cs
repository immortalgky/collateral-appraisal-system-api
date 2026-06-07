namespace Workflow.Workflow.Events;

/// <summary>
/// Raised when an approval round resolves (advances) before every member has voted — i.e. in
/// <c>Quorum</c> voting mode once quorum + majority are met, or on an immediate <c>route_back</c>.
/// The handler closes out the still-open per-member <c>PendingTask</c> rows for this activity so
/// the tasks of members who never voted do not dangle. Voters' tasks are already removed by
/// <see cref="TaskCompletedDomainEvent"/>, so only non-voters remain.
/// </summary>
public record ApprovalRoundClosedEvent(
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime ClosedAt,
    string Reason = "Closed"
) : IDomainEvent;
