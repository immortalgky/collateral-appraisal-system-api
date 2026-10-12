namespace Reporting.Application.OperationalReports.Rcas008;

/// <summary>
/// One row of RCAS008 (external company service-quality scores). Positional: the view's column order must match
/// (<c>AppraisalCompanyEn</c> is the English name the company filter also matches; the report shows
/// <c>AppraisalCompany</c>).
/// </summary>
public sealed record Rcas008Row(
    Guid Id,
    string? AppraisalNumber,
    string? AppraisalCompany,
    string? AppraisalCompanyEn,
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
