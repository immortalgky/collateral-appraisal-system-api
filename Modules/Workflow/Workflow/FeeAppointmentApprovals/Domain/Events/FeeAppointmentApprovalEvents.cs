namespace Workflow.FeeAppointmentApprovals.Domain.Events;

public record FeeAppointmentApprovalRaisedDomainEvent(
    Guid ApprovalId,
    Guid AppraisalId,
    string ApproverAssignee,
    string AssignedType) : IDomainEvent;

public record FeeAppointmentApprovalResolvedDomainEvent(
    Guid ApprovalId,
    Guid AppraisalId,
    IReadOnlyList<(string LineType, Guid TargetId, string Decision, string? Reason)> LineOutcomes,
    string? ResolvedByCode) : IDomainEvent;

public record FeeAppointmentApprovalCancelledDomainEvent(
    Guid ApprovalId,
    Guid AppraisalId,
    string Reason) : IDomainEvent;
