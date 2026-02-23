namespace Appraisal.Application.Features.Assignments.GetAssignments;

public record GetAssignmentsResult(List<AssignmentDto> Assignments);

public record AssignmentDto(
    Guid Id,
    Guid AppraisalId,
    string AssignmentType,
    string AssignmentStatus,
    string? AssigneeUserId,
    string? AssigneeCompanyId,
    string? InternalAppraiserId,
    string AssignmentMethod,
    int ReassignmentNumber,
    int ProgressPercent,
    DateTime AssignedAt,
    string AssignedBy,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CreatedAt);