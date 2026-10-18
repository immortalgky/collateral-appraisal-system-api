namespace Reporting.Application.OperationalReports.Rcas009;

/// <summary>
/// One row of RCAS009 (appraisal-fee summary for Accounting). Positional: the view's column order must match
/// (<c>AppraisalCompanyEn</c> is the English name the company filter also matches; the report shows
/// <c>AppraisalCompany</c>).
/// </summary>
public sealed record Rcas009Row(
    Guid Id,
    string? AppraisalNumber,
    string? CustomerName,
    string? AssignType,
    string? PayType,
    string? PayTypeCode,
    string? Purpose,
    DateTime? AppraisalCreateDate,
    string? CollateralType,
    string? AppraisalStatus,
    string? RequestorCode,
    string? RequestorDepartment,
    string? BankingSegment,
    string? AppraisalCompany,
    string? AppraisalCompanyEn,
    string? InternalAppraisalStaff,
    string? InvoiceNumber,
    string? CostCenter,
    decimal? AppraisalFee,
    decimal? VAT,
    decimal? IncludeVAT,
    string? FeeStatus);
