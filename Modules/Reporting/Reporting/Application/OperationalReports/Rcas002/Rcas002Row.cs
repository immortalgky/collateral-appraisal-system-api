namespace Reporting.Application.OperationalReports.Rcas002;

/// <summary>One row of RCAS002 (Collateral review-due by type).</summary>
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
    int? RemainingDays);
