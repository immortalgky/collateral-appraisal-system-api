namespace Reporting.Application.OperationalReports.Rcas008;

/// <summary>One row of RCAS008 (external company service-quality scores).</summary>
public sealed record Rcas008Row(
    Guid Id,
    string? AppraisalNumber,
    string? AppraisalCompany,
    DateTime? ApprovedDate,
    string? BankingSegment,
    decimal? TotalScorePct,
    int? ScoreReportQuality,
    int? ScoreDeliveryTime,
    int? ScorePersonnel,
    int? ScoreResponseTime,
    int? ScoreCoordination,
    string? Remark,
    string? EvaluationStatus,
    string? InternalAppraisalStaff,
    string? PurposeCode,
    string? AppraisalType);
