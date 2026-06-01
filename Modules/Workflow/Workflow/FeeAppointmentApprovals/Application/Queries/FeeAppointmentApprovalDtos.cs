namespace Workflow.FeeAppointmentApprovals.Application.Queries;

public record FeeAppointmentApprovalDto(
    Guid Id,
    Guid AppraisalId,
    string RequestSource,
    string Status,
    string? ResolvedTier,
    string? ApproverAssignee,
    string? AssignedType,
    Guid? FollowupWorkflowInstanceId,
    DateTime RaisedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<FeeApprovalLineDto> Lines);

public record FeeApprovalLineDto(
    Guid Id,
    string LineType,
    Guid TargetId,
    DateTime? NewDate,
    int? RescheduleCount,
    string? FeeCode,
    string? FeeDescription,
    decimal? FeeAmount,
    string LineStatus,
    string? DecisionReason);
