namespace Reporting.Application.OperationalReports.Rcas004;

/// <summary>One row of RCAS004 (construction inspection &lt; 100%).</summary>
public sealed record Rcas004Row(
    Guid Id,
    DateTime? AppraisalCreateDate,
    string? AppraisalNumber,
    string? CustomerName,
    string? Purpose,
    decimal? ApplyLimitAmount,
    string? CollateralType,
    string? Channel,
    string? AppraisalCompany,
    string? InternalAppraisalStaff,
    decimal? AppraisalValue,
    string? PreviousAppraisalNumber,
    DateTime? AppointmentDate,
    string? AppraisalStatus,
    decimal? ProgressiveInspectionPct);
