using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Ola;

/// <summary>Common filter for the OLA reports (RCAS003/005/006/011).</summary>
public sealed record OlaFilter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Status,
    string? AppraisalCompany,
    string? InternalStaff,
    string? Channel,
    string? AppraisalNumber,
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

    public static ReportDefinition<OlaReportRow, OlaFilter> Create(
        string baseName, string title, string? assignmentScope, IOlaTimingService ola) => new()
    {
        BaseName = baseName,
        Title = title,
        Columns = Columns,
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalNumber"),
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
        ],
        EnrichAsync = (rows, ct) => EnrichAsync(rows, ola, ct),
    };

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
        // column now emits the resolved "First Last" name for display, so binding the code against it
        // would never match.
        ReportFilterSql.Exact(c, p, f.InternalStaff, "InternalAppraisalStaffCode", "InternalStaff");
        ReportFilterSql.Exact(c, p, f.Channel, "Channel", "Channel");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");

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
    private static readonly IReadOnlyList<ReportColumn<OlaReportRow>> Columns =
    [
        new("Appraisal No.", r => r.AppraisalNumber),
        new("Customer Name", r => r.CustomerName),
        new("Purpose", r => r.Purpose),
        new("Apply/Limit Amount", r => r.ApplyLimitAmount, ColumnFormat.Money, Total: true),
        new("Collateral Type", r => r.CollateralType),
        new("Channel", r => r.Channel),
        new("Appraisal Company", r => r.AppraisalCompany),
        new("Internal Staff", r => r.InternalAppraisalStaff),
        new("Appointment Date", r => r.AppointmentDate, ColumnFormat.DateTime),
        new("Assign Date", r => r.AssignDate, ColumnFormat.DateTime),
        new("Receive Date", r => r.ReceiveDate, ColumnFormat.DateTime),
        new("OLA Appraisal (hrs)", r => r.OlaAppraisal, ColumnFormat.Decimal),
        new("OLA Staff/Verify (hrs)", r => r.OlaInternalStaffVerify, ColumnFormat.Decimal),
        new("OLA Checker (hrs)", r => r.OlaInternalChecker, ColumnFormat.Decimal),
        new("OLA Staff+Checker (hrs)", r => r.OlaInternalStaffPlusChecker, ColumnFormat.Decimal),
        new("OLA Verify (hrs)", r => r.OlaInternalVerify, ColumnFormat.Decimal),
        new("OLA Approval (hrs)", r => r.OlaApproval, ColumnFormat.Decimal),
        new("Status", r => r.AppraisalStatus),
    ];
}
