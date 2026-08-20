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
    // Thai name (null when absent); the client picks by its own locale. Position is load-bearing —
    // it must stay directly after AppraiserCompanyName to match the handler's SELECT order.
    string? AppraiserCompanyNameLocal,
    decimal? AppraisalValue,
    Guid? EvaluationId,
    string? EvaluationStatus,
    decimal? TotalScore,
    string? InternalFollowupStaffId,
    string? InternalFollowupStaffName
);
