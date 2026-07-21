using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas007;

/// <summary>
/// Selection criteria shared by RCAS007 (SLA summary) and RCAS012 (company follow-up, SLA &gt; 2 days).
/// RCAS012 uses the subset it needs; the rest stay null.
/// </summary>
public sealed record Rcas007Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Status,
    string? AppraisalCompany,
    string? InternalStaff,
    string? AppraisalNumber,
    string? Purpose,
    string? CustomerName,
    string? ExternalStaff,
    string? SortBy,
    string? SortDir);

/// <summary>
/// Builds the SLA reports. RCAS007 and RCAS012 share the same row, view, and business-day SLA
/// computation; RCAS012 differs only by title, the Retail/IBG column position, and a hard
/// "SLA &gt; 2 days" post-filter. The SLA value is enriched in C# (see <see cref="IReportSlaCalculator"/>).
/// FSD lists no sign-off footer for these. Totals: Appraisal fee + Appraisal Value.
/// </summary>
internal static class SlaReport
{
    private const decimal FollowUpThresholdDays = 2m;

    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "AppraisalCreateDate", "AppraisalCompany", "AppraisalStatus"
    };

    /// <param name="slaOver2DaysOnly">RCAS012: keep only rows whose SLA exceeds 2 business days.</param>
    /// <param name="retailIbgEarly">RCAS012 places Retail/IBG at field 4 (before Requestor).</param>
    public static ReportDefinition<Rcas007Row, Rcas007Filter> Create(
        string baseName, string title, string? assignmentScope, IReportSlaCalculator sla,
        bool slaOver2DaysOnly = false, bool retailIbgEarly = false) => new()
    {
        BaseName = baseName,
        Title = title,
        Columns = retailIbgEarly ? ColumnsRetailEarly : ColumnsDefault,
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalNumber"),
        Build = f => Build(f, assignmentScope),
        DescribeFilter = f =>
        [
            new("Created From", f.CreatedFrom?.ToString("yyyy-MM-dd")),
            new("Created To", f.CreatedTo?.ToString("yyyy-MM-dd")),
            new("Status", f.Status),
            new("Appraisal Company", f.AppraisalCompany),
            new("Internal Staff", f.InternalStaff),
            new("Appraisal No.", f.AppraisalNumber),
            new("Purpose", f.Purpose, "AppraisalPurpose"),
            new("Customer Name", f.CustomerName),
            new("External Staff", f.ExternalStaff),
        ],
        EnrichAsync = async (rows, ct) =>
        {
            if (rows.Count == 0) return;
            var map = await sla.ComputeAsync(
                rows.Select(r => new SlaInput(r.Id, r.AppointmentDate, r.SubmittedAt)).ToList(), ct);
            foreach (var r in rows)
                if (map.TryGetValue(r.Id, out var days)) r.Sla = days;
        },
        PostEnrichFilter = slaOver2DaysOnly ? r => r.Sla > FollowUpThresholdDays : null,
    };

    private static (string, DynamicParameters) Build(Rcas007Filter f, string? assignmentScope)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(assignmentScope))
        {
            c.Add("AssignmentType = @AssignmentScope");
            p.Add("AssignmentScope", assignmentScope);
        }

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "AppraisalCreateDate", "Created");
        ReportFilterSql.MultiValue(c, p, f.Status, "AppraisalStatus", "Statuses");
        ReportFilterSql.Contains(c, p, f.AppraisalCompany, "AppraisalCompany", "AppraisalCompany");
        ReportFilterSql.Exact(c, p, f.InternalStaff, "InternalAppraisalStaffCode", "InternalStaff");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");
        ReportFilterSql.MultiValue(c, p, f.Purpose, "PurposeCode", "Purposes");
        ReportFilterSql.Contains(c, p, f.CustomerName, "CustomerName", "CustomerName");
        // Binds the raw code, like InternalStaff — ExternalStaffName is the display column.
        ReportFilterSql.Exact(c, p, f.ExternalStaff, "ExternalStaffCode", "ExternalStaff");

        return ("SELECT * FROM reporting.vw_RCAS007_SlaSummary" + ReportFilterSql.Where(c), p);
    }

    private static readonly IReadOnlyList<ReportColumn<Rcas007Row>> ColumnsDefault = BuildColumns(false);
    private static readonly IReadOnlyList<ReportColumn<Rcas007Row>> ColumnsRetailEarly = BuildColumns(true);

    private static IReadOnlyList<ReportColumn<Rcas007Row>> BuildColumns(bool retailIbgEarly) =>
    [
        new("Appraisal No.", r => r.AppraisalNumber),
        new("Customer Name", r => r.CustomerName),
        new("Purpose", r => r.Purpose),
        .. retailIbgEarly
            ? new ReportColumn<Rcas007Row>[] { new("Retail/IBG", r => r.BankingSegment) }
            : [],
        new("Requestor", r => r.RequestorName),
        new("Requestor Phone", r => r.RequestorPhone),
        new("Requestor Dept.", r => r.RequestorDepartment),
        .. retailIbgEarly
            ? []
            : new ReportColumn<Rcas007Row>[] { new("Retail/IBG", r => r.BankingSegment) },
        new("Appraisal Company", r => r.AppraisalCompany),
        new("External Staff", r => r.ExternalStaffName),
        new("Company Phone", r => r.AppraisalCompanyPhone),
        new("Internal Staff", r => r.InternalAppraisalStaff),
        new("Internal Staff Phone", r => r.InternalAppraisalStaffPhone),
        new("Appraisal Fee", r => r.AppraisalFee, ColumnFormat.Money, Total: true),
        new("Created Date", r => r.AppraisalCreateDate, ColumnFormat.DateTime),
        new("Appointment Date", r => r.AppointmentDate, ColumnFormat.DateTime),
        new("SLA (days)", r => r.Sla, ColumnFormat.Decimal),
        new("Appraisal Value", r => r.AppraisalValue, ColumnFormat.Money, Total: true),
        new("Current Role", r => r.Role),
        new("Status", r => r.AppraisalStatus),
    ];
}
