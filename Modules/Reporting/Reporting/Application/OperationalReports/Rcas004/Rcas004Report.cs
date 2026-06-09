using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas004;

public sealed record Rcas004Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Status,
    string? SortBy,
    string? SortDir);

/// <summary>RCAS004 — รายงานการตรวจงวดงานที่ยังไม่ครบ 100 % (construction inspection &lt; 100%).</summary>
internal static class Rcas004Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "AppraisalCreateDate", "ProgressiveInspectionPct", "AppointmentDate"
    };

    public static readonly ReportDefinition<Rcas004Row, Rcas004Filter> Definition = new()
    {
        BaseName = "RCAS004-ConstructionInspection",
        Title = "รายงานการตรวจงวดงานที่ยังไม่ครบ 100 %",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalNumber"),
        Build = Build,
        Columns =
        [
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Customer Name", r => r.CustomerName),
            new("Purpose", r => r.Purpose),
            new("Apply/Limit Amount", r => r.ApplyLimitAmount, ColumnFormat.Money),
            new("Collateral Type", r => r.CollateralType),
            new("Channel", r => r.Channel),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Internal Staff", r => r.InternalAppraisalStaff),
            new("Appraisal Value", r => r.AppraisalValue, ColumnFormat.Money),
            new("Previous Appraisal No.", r => r.PreviousAppraisalNumber),
            new("Appointment Date", r => r.AppointmentDate, ColumnFormat.DateTime),
            new("Status", r => r.AppraisalStatus),
            new("Inspection %", r => r.ProgressiveInspectionPct, ColumnFormat.Percent),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas004Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "AppraisalCreateDate", "Created");
        ReportFilterSql.MultiValue(c, p, f.Status, "AppraisalStatus", "Statuses");

        return ("SELECT * FROM reporting.vw_RCAS004_ConstructionInspection" + ReportFilterSql.Where(c), p);
    }
}
