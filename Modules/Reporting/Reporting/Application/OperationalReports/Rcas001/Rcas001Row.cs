namespace Reporting.Application.OperationalReports.Rcas001;

/// <summary>One row of RCAS001 (Appraisal books by period/status/department).</summary>
public sealed record Rcas001Row(
    Guid Id,
    DateTime? AppraisalCreateDate,
    string? AppraisalNumber,
    string? CustomerName,
    string? AppraisalPurpose,
    decimal? ApplyLimitAmount,
    string? CollateralType,
    string? ApproachMethod,
    decimal? AppraisalPrice,
    string? AppraisalStatus,
    string? RequestorCode,
    string? RequestorDepartment,
    string? BankingSegment,
    string? InternalAppraisalStaff,
    string? AppraisalCompany,
    DateTime? ApproveDate)
{
    /// <summary>FSD "Running No." (Running Record). Assigned post-sort by the report's enrichment;
    /// not a SQL column, so it sits outside the positional (Dapper-mapped) constructor.</summary>
    public int? RunningNo { get; set; }
}
