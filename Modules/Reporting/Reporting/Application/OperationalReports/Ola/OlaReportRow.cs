namespace Reporting.Application.OperationalReports.Ola;

/// <summary>
/// Shared row for the OLA reports (RCAS003/005/006/011). Base columns are filled by Dapper from
/// <c>reporting.vw_RCAS_OlaBase</c>; the OLA* business-hour columns + ReceiveDate are filled by the
/// post-query enrichment (<see cref="Shared.IOlaTimingService"/>). Mutable by design (enrichment writes).
/// </summary>
public sealed class OlaReportRow
{
    // --- From SQL (vw_RCAS_OlaBase) ---
    public Guid Id { get; set; }
    public Guid? RequestId { get; set; }
    public DateTime? AppraisalCreateDate { get; set; }
    public string? AppraisalNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? Purpose { get; set; }
    public decimal? ApplyLimitAmount { get; set; }
    public string? CollateralType { get; set; }
    public string? Channel { get; set; }
    public string? AssignmentType { get; set; }
    public string? AppraisalCompany { get; set; }
    public string? InternalAppraisalStaff { get; set; }
    public string? RequestorCode { get; set; }
    public string? BankingSegment { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public DateTime? AssignDate { get; set; }
    public string? AppraisalStatus { get; set; }

    // --- Enriched (business hours; weekends/holidays/lunch excluded) ---
    public DateTime? ReceiveDate { get; set; }
    public decimal? OlaAppraisal { get; set; }
    public decimal? OlaInternalStaffVerify { get; set; }
    public decimal? OlaInternalChecker { get; set; }
    public decimal? OlaInternalStaffPlusChecker { get; set; }
    public decimal? OlaInternalVerify { get; set; }
    public decimal? OlaApproval { get; set; }
}
