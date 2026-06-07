namespace Reporting.Application.OperationalReports.Rcas009;

/// <summary>One row of RCAS009 (appraisal-fee summary for Accounting).</summary>
public sealed record Rcas009Row(
    Guid Id,
    string? AppraisalNumber,
    string? CustomerName,
    string? AssignType,
    string? PayType,
    string? Purpose,
    DateTime? AppraisalCreateDate,
    string? CollateralType,
    string? AppraisalStatus,
    string? RequestorCode,
    string? RequestorDepartment,
    string? BankingSegment,
    string? AppraisalCompany,
    string? InternalAppraisalStaff,
    string? InvoiceNumber,
    string? CostCenter,
    decimal? AppraisalFee,
    decimal? VAT,
    decimal? IncludeVAT,
    string? FeeStatus);
