namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public record PendingEvaluationDto(
    Guid AppraisalId,
    string? AppraisalNumber,
    string? AppraisalStatus,
    string? CustomerName,
    DateTime? ReportReceivedDate,
    string? ExternalAppraiserName,
    string? AssigneeCompanyId,
    string? AppraiserCompanyName,
    decimal? AppraisalValue,
    Guid? EvaluationId,
    string? EvaluationStatus,
    decimal? TotalScore,
    string? InternalFollowupStaffId,
    string? InternalFollowupStaffName
);
