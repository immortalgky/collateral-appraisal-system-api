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
    string? InternalFollowupAssignmentMethod,
    string AssignmentMethod,
    int ReassignmentNumber,
    int ProgressPercent,
    DateTime? AssignedAt,
    string AssignedBy,
    DateTime? StartedAt,
    DateTime? SubmittedAt,
    DateTime? CompletedAt,
    string? RejectionReason,
    string? CancellationReason,
    string? Remark,
    DateTime? DraftSavedAt,
    DateTime? CreatedAt,
    List<EngagementCycleDto> Cycles,
    int TotalExternalBusinessMinutes,
    int SubmissionCount,
    /// <summary>
    /// For an off-system external engagement (AssignmentMethod = "Offline"), the appraisal date
    /// keyed off the company's paper book — i.e. ValuationAnalyses.ValuationDate. Null on every
    /// other path, where the date is derived from the appointment instead. The keyin screen needs
    /// this to show what was already recorded; without it a keyer returning to correct the company
    /// sees an empty date field and re-keys today's date over the real one.
    /// </summary>
    DateTime? OfflineBookDate);

public record EngagementCycleDto(
    Guid Id,
    int CycleNumber,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    int? BusinessMinutes,
    string Status);