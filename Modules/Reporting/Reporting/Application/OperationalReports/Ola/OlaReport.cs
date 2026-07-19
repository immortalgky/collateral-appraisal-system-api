using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Ola;

/// <summary>
/// Common filter for the OLA reports (RCAS003/005/006/011). The union of every OLA report's FSD
/// selection criteria; each is optional (soft-default All), so a report that doesn't expose one
/// simply leaves it null. (FSD's "Customer Number" for RCAS003 is satisfied by CustomerName — the
/// system stores no customer/CIF number, only the customer name.)
/// </summary>
public sealed record OlaFilter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Status,
    string? AppraisalCompany,
    string? InternalStaff,
    string? Channel,
    string? AppraisalNumber,
    string? CustomerName,
    string? Purpose,
    string? AoCode,
    string? ExternalStaff,
    string? DepartmentCode,
    string? SortBy,
    string? SortDir);

/// <summary>
/// Builds the OLA report definitions. RCAS003/005/006/011 share columns + enrichment and differ only
/// by name/title and an optional AssignmentType scope (External for 005, Internal for 006).
/// The OLA segments are computed in C# (see <see cref="IOlaTimingService"/>) — the SQL only projects
/// the base row, so a date filter on the base view is sufficient.
/// </summary>
internal static class OlaReport
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "AppraisalCreateDate", "AppraisalCompany", "InternalAppraisalStaff", "AppraisalStatus"
    };

    /// <param name="fsdDetail">
    /// True for the four true OLA reports (RCAS003/005/006/011), whose FSD field tables include the
    /// Appraisal Create Date + Role columns and the Print/Approve Report By sign-off footer.
    /// False for RCAS007/012, which share this code but whose FSD tables list neither.
    /// </param>
    /// <param name="defaultSort">
    /// The report's FSD sort sequence (composite allowed), e.g. RCAS005 = Appraisal Company then
    /// Appraisal Report Number, RCAS006 = Internal Appraisal Staff. Defaults to Appraisal Number.
    /// </param>
    /// <param name="internalColumns">
    /// RCAS006 only: its FSD field table omits Receive Appraisal Report Date and OLA Internal Staff
    /// (Verify) (internal work has no company→bank handoff), so those two columns are dropped.
    /// </param>
    public static ReportDefinition<OlaReportRow, OlaFilter> Create(
        string baseName, string title, string? assignmentScope, IOlaTimingService ola,
        bool fsdDetail = false, IReadOnlyList<string>? defaultSort = null, bool internalColumns = false)
    {
        var sort = defaultSort ?? ["AppraisalNumber"];
        return new()
        {
            BaseName = baseName,
            Title = title,
            Columns = fsdDetail ? (internalColumns ? ColumnsFsdInternal : ColumnsFsd) : ColumnsBase,
            IncludeSignoffFooter = fsdDetail,
            OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, sort),
            Build = f => Build(f, assignmentScope),
            DescribeFilter = f =>
            [
                new("Created From", f.CreatedFrom?.ToString("yyyy-MM-dd")),
                new("Created To", f.CreatedTo?.ToString("yyyy-MM-dd")),
                new("Status", f.Status),
                new("Appraisal Company", f.AppraisalCompany),
                new("Internal Staff", f.InternalStaff),
                new("Channel", f.Channel, "Channel"),
                new("Appraisal No.", f.AppraisalNumber),
                new("Customer Name", f.CustomerName),
                new("Purpose", f.Purpose, "AppraisalPurpose"),
                new("AO Code", f.AoCode),
                new("External Staff", f.ExternalStaff),
                new("Department Code", f.DepartmentCode),
            ],
            EnrichAsync = (rows, ct) => EnrichAsync(rows, ola, ct),
        };
    }

    private static (string, DynamicParameters) Build(OlaFilter f, string? assignmentScope)
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
        // Filter on the raw assignee code (InternalAppraisalStaffCode); the InternalAppraisalStaff
        // column now emits the resolved "CODE - First Last" for display, so binding the code against it
        // would never match.
        ReportFilterSql.Exact(c, p, f.InternalStaff, "InternalAppraisalStaffCode", "InternalStaff");
        ReportFilterSql.MultiValue(c, p, f.Channel, "Channel", "Channels");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");
        // Free-text name search — there is no customer/CIF number column to bind an exact match to.
        ReportFilterSql.Contains(c, p, f.CustomerName, "CustomerName", "CustomerName");
        // Purpose binds the raw code (PurposeCode); the Purpose column emits the resolved description.
        ReportFilterSql.MultiValue(c, p, f.Purpose, "PurposeCode", "Purposes");
        ReportFilterSql.Exact(c, p, f.AoCode, "RequestorAoCode", "AoCode");
        ReportFilterSql.Exact(c, p, f.ExternalStaff, "ExternalStaffCode", "ExternalStaff");
        ReportFilterSql.Exact(c, p, f.DepartmentCode, "RequestorDepartment", "DepartmentCode");

        return ("SELECT * FROM reporting.vw_RCAS_OlaBase" + ReportFilterSql.Where(c), p);
    }

    private static async Task EnrichAsync(
        IReadOnlyList<OlaReportRow> rows, IOlaTimingService ola, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        var inputs = rows.Select(r => new OlaInput(r.Id, r.RequestId, r.AppointmentDate)).ToList();
        var map = await ola.ComputeAsync(inputs, ct);

        foreach (var r in rows)
        {
            if (!map.TryGetValue(r.Id, out var t)) continue;
            r.ReceiveDate = t.ReceiveDate;
            r.OlaAppraisal = t.OlaAppraisal;
            r.OlaInternalStaffVerify = t.OlaInternalStaffVerify;
            r.OlaInternalChecker = t.OlaInternalChecker;
            r.OlaInternalStaffPlusChecker = t.OlaInternalStaffPlusChecker;
            r.OlaInternalVerify = t.OlaInternalVerify;
            r.OlaApproval = t.OlaApproval;
        }
    }

    // Durations are business HOURS. Total row on Apply/Limit Amount.
    // Built once per variant: Create() runs per request, so don't rebuild the delegates each call.
    //   Base           — RCAS007/012 (no Created Date / Role).
    //   Fsd            — RCAS003/005/011 (adds Created Date + Role; full OLA segment set).
    //   FsdInternal    — RCAS006 (its FSD omits Report Received Date + OLA Internal Staff (Verify)).
    private static readonly IReadOnlyList<ReportColumn<OlaReportRow>> ColumnsBase = BuildColumns(false, false);
    private static readonly IReadOnlyList<ReportColumn<OlaReportRow>> ColumnsFsd = BuildColumns(true, false);
    private static readonly IReadOnlyList<ReportColumn<OlaReportRow>> ColumnsFsdInternal = BuildColumns(true, true);

    private static IReadOnlyList<ReportColumn<OlaReportRow>> BuildColumns(bool fsdDetail, bool internalColumns) =>
    [
        new("Appraisal No.", r => r.AppraisalNumber),
        new("Customer Name", r => r.CustomerName),
        new("Purpose", r => r.Purpose),
        new("Apply/Limit Amount", r => r.ApplyLimitAmount, ColumnFormat.Money, Total: true),
        new("Collateral Type", r => r.CollateralType),
        // FSD order: Appraisal Create Date sits between Collateral Type and Channel, and is
        // "Display Date & Time" (matching Rcas001Report; RCAS009's Date-only is the outlier).
        .. fsdDetail
            ? new ReportColumn<OlaReportRow>[]
                { new("Created Date", r => r.AppraisalCreateDate, ColumnFormat.DateTime) }
            : [],
        new("Channel", r => r.Channel),
        new("Appraisal Company", r => r.AppraisalCompany),
        new("Internal Staff", r => r.InternalAppraisalStaff),
        .. fsdDetail
            ? new ReportColumn<OlaReportRow>[] { new("Role", r => r.Role) }
            : [],
        new("Appointment Date", r => r.AppointmentDate, ColumnFormat.DateTime),
        new("Assigned Date", r => r.AssignDate, ColumnFormat.DateTime),
        // RCAS006's FSD table omits Report Received Date and OLA Internal Staff (Verify).
        .. internalColumns
            ? []
            : new ReportColumn<OlaReportRow>[]
                { new("Report Received Date", r => r.ReceiveDate, ColumnFormat.DateTime) },
        new("OLA Appraisal (hrs)", r => r.OlaAppraisal, ColumnFormat.Decimal),
        .. internalColumns
            ? []
            : new ReportColumn<OlaReportRow>[]
                { new("OLA Internal Staff (Verify) (hrs)", r => r.OlaInternalStaffVerify, ColumnFormat.Decimal) },
        new("OLA Internal Checker (hrs)", r => r.OlaInternalChecker, ColumnFormat.Decimal),
        new("OLA (Internal Staff + Internal Checker) (hrs)", r => r.OlaInternalStaffPlusChecker, ColumnFormat.Decimal),
        new("OLA Internal Verify (hrs)", r => r.OlaInternalVerify, ColumnFormat.Decimal),
        new("OLA Approval (hrs)", r => r.OlaApproval, ColumnFormat.Decimal),
        new("Status", r => r.AppraisalStatus),
    ];
}
