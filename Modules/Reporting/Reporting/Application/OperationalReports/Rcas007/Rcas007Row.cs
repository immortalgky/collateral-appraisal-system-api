namespace Reporting.Application.OperationalReports.Rcas007;

/// <summary>
/// One row of RCAS007 (SLA summary). A mutable class (not a positional record) so Dapper maps by
/// name and the post-query enrichment can write the computed <see cref="Sla"/>. Shared shape reused
/// by RCAS012.
/// </summary>
public sealed class Rcas007Row
{
    // --- From SQL (vw_RCAS007_SlaSummary) ---
    public Guid Id { get; set; }
    public string? AppraisalNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? Purpose { get; set; }
    public string? RequestorName { get; set; }
    public string? RequestorPhone { get; set; }
    public string? RequestorDepartment { get; set; }
    public string? BankingSegment { get; set; }
    public string? AppraisalCompany { get; set; }
    public string? ExternalStaffName { get; set; }
    public string? AppraisalCompanyPhone { get; set; }
    public string? InternalAppraisalStaff { get; set; }
    public string? InternalAppraisalStaffPhone { get; set; }
    public decimal? AppraisalFee { get; set; }
    public DateTime? AppraisalCreateDate { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal? AppraisalValue { get; set; }
    public string? Role { get; set; }
    public string? AppraisalStatus { get; set; }

    // --- Enriched (business-day SLA elapsed) ---
    public decimal? Sla { get; set; }
}
