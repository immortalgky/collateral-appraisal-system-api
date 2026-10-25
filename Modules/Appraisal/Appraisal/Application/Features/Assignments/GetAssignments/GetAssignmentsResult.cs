using System.Text.Json.Serialization;

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
    DateTime? OfflineBookDate,
    // Display names of the employee codes this DTO carries (AssignedBy, AssigneeUserId, InternalAppraiserId — the
    // follow-up staff), resolved in one IUserLookupService call; null when the code is empty or unknown. The API drops
    // null properties app-wide; pinned so the front end always receives the keys.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AssignedByName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AssigneeUserName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? InternalAppraiserName = null);

public record EngagementCycleDto(
    Guid Id,
    int CycleNumber,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    int? BusinessMinutes,
    string Status);