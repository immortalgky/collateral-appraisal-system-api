namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

/// <summary>
/// One row per appraisal currently awaiting at least one pending committee vote.
/// Sources from common.vw_MonitoringPendingApprovals.
/// </summary>
public record MeetingFollowupDto(
    Guid AppraisalId,
    string AppraisalNumber,
    string? CustomerName,
    int ApprovalTier,
    int PendingCount,
    int TotalApprovers,
    DateTime? EarliestDueAt,
    string? WorstSlaStatus,
    // Tier 3 only — null for tiers 1 and 2
    Guid? MeetingId,
    string? MeetingNumber,
    DateTime? MeetingDate,
    string? MeetingStatus
);
