using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas001;

/// <summary>Selection criteria for RCAS001.</summary>
public sealed record Rcas001Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Status,
    string? BankingSegment,
    string? AppraisalNumber,
    string? SortBy,
    string? SortDir);

/// <summary>
/// RCAS001 — รายงานเล่มประเมินตามช่วงเวลา &amp; ตามสถานะของงาน &amp; ตามฝ่ายงาน.
/// Appraisal books by create-date / status / department.
/// </summary>
internal static class Rcas001Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalCreateDate", "AppraisalNumber", "CustomerName", "ApplyLimitAmount",
        "AppraisalStatus", "ApproveDate", "AppraisalCompany", "BankingSegment"
    };

    public static readonly ReportDefinition<Rcas001Row, Rcas001Filter> Definition = new()
    {
        BaseName = "RCAS001-AppraisalBooks",
        Title = "รายงานเล่มประเมินตามช่วงเวลา & ตามสถานะของงาน & ตามฝ่ายงาน",
        Build = Build,
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalNumber"),
        DescribeFilter = f =>
        [
            new("Created From", f.CreatedFrom?.ToString("yyyy-MM-dd")),
            new("Created To", f.CreatedTo?.ToString("yyyy-MM-dd")),
            new("Status", f.Status),
            new("Retail/IBG", f.BankingSegment, "BankingSegment"),
            new("Appraisal No.", f.AppraisalNumber),
        ],
        Columns =
        [
            new("Appraisal Create Date", r => r.AppraisalCreateDate, ColumnFormat.DateTime),
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Customer Name", r => r.CustomerName),
            new("Purpose", r => r.AppraisalPurpose),
            new("Apply/Limit Amount", r => r.ApplyLimitAmount, ColumnFormat.Money, Total: true),
            new("Collateral Type", r => r.CollateralType),
            new("Approach Method", r => r.ApproachMethod),
            new("Appraisal Price", r => r.AppraisalPrice, ColumnFormat.Money),
            new("Status", r => r.AppraisalStatus),
            new("Requestor", r => r.RequestorCode),
            new("Requestor Dept.", r => r.RequestorDepartment),
            new("Retail/IBG", r => r.BankingSegment),
            new("Internal Staff", r => r.InternalAppraisalStaff),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Approve Date", r => r.ApproveDate, ColumnFormat.DateTime),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas001Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "AppraisalCreateDate", "Created");
        ReportFilterSql.MultiValue(c, p, f.Status, "AppraisalStatus", "Statuses");
        ReportFilterSql.Exact(c, p, f.BankingSegment, "BankingSegment", "BankingSegment");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");

        return ("SELECT * FROM reporting.vw_RCAS001_AppraisalBooks" + ReportFilterSql.Where(c), p);
    }
}
