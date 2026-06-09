using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas002;

public sealed record Rcas002Filter(
    string? ReviewType,
    string? Stage,
    string? CustomerName,
    string? SortBy,
    string? SortDir);

/// <summary>RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท (collateral review-due by type).</summary>
internal static class Rcas002Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReviewType", "RemainingDays", "AppraisalNumber", "ValuationDate", "PastDueDay"
    };

    public static readonly ReportDefinition<Rcas002Row, Rcas002Filter> Definition = new()
    {
        BaseName = "RCAS002-ReappraisalDue",
        Title = "รายงานการครบกำหนดทบทวนหลักประกันตามประเภท",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "RemainingDays"),
        Build = Build,
        Columns =
        [
            new("Review Type", r => r.ReviewType),
            new("Stage", r => r.Stage),
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Previous Appraisal No.", r => r.PreviousAppraisalNumber),
            new("Collateral No.", r => r.CollateralNumber),
            new("CIF Number", r => r.CifNumber),
            new("Customer Name", r => r.CustomerName),
            new("Apply/Limit Amount", r => r.ApplyLimitAmount, ColumnFormat.Money),
            new("Collateral Type", r => r.CollateralType),
            new("Title Deed No.", r => r.TitleDeedNumber),
            new("Retail/IBG", r => r.BankingSegment),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Internal Staff", r => r.InternalAppraisalStaff),
            new("Old Appraisal Value", r => r.OldAppraisalValue, ColumnFormat.Money),
            new("Past Due Day", r => r.PastDueDay, ColumnFormat.Integer),
            new("Valuation Date", r => r.ValuationDate, ColumnFormat.Date),
            new("Next Valuation Date", r => r.NextValuationDate, ColumnFormat.Date),
            new("Remaining Days", r => r.RemainingDays, ColumnFormat.Integer),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas002Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.MultiValue(c, p, f.ReviewType, "ReviewType", "ReviewTypes");
        ReportFilterSql.MultiValue(c, p, f.Stage, "Stage", "Stages");
        ReportFilterSql.Contains(c, p, f.CustomerName, "CustomerName", "CustomerName");

        return ("SELECT * FROM reporting.vw_RCAS002_ReappraisalDue" + ReportFilterSql.Where(c), p);
    }
}
