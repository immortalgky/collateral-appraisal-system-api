namespace Appraisal.Application.Features.Assignments.GetAssignments;

public record GetAssignmentsResult(List<AssignmentDto> Assignments);

public record AssignmentDto(
    Guid Id,
    Guid AppraisalId,
    string AssignmentMode,
    string AssignmentStatus,
    Guid? AssigneeUserId,
    Guid? AssigneeCompanyId,
    string? ExternalAppraiserName,
    string? ExternalAppraiserLicense,
    string AssignmentSource,
    int ReassignmentNumber,
    int ProgressPercent,
    DateTime AssignedAt,
    Guid AssignedBy,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CreatedOn);
