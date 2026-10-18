namespace Reporting.Application.OperationalReports.Rcas002;

/// <summary>
/// One row of RCAS002 (Collateral review-due by type) — one AS400 book. Positional: the view's column
/// order must match, so new columns are appended at the end of both.
/// </summary>
public sealed record Rcas002Row(
    Guid Id,
    string? ReviewType,
    string? Stage,
    string? AppraisalNumber,
    string? PreviousAppraisalNumber,
    string? CollateralNumber,
    string? CifNumber,
    string? CustomerName,
    decimal? ApplyLimitAmount,
    string? CollateralType,
    string? TitleDeedNumber,
    string? BankingSegment,
    string? AppraisalCompany,
    string? InternalAppraisalStaff,
    decimal? OldAppraisalValue,
    int? PastDueDay,
    DateTime? ValuationDate,
    DateTime? NextValuationDate,
    int? RemainingDays,
    string? ReviewTypeCode,
    string? ReviewStatus,
    string? NewAppraisalNumber,
    DateTime? NewAppraisalSubmittedAt,
    DateTime? NewAppraisalCompletedAt,
    string? NewAppraisalStatus,
    string? ReviewStatusCode);
