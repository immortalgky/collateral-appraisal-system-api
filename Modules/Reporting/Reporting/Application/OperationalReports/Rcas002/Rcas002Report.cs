using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas002;

public sealed record Rcas002Filter(
    string? ReviewType,
    string? Stage,
    string? CustomerName,
    string? AppraisalNumber,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? SortBy,
    string? SortDir);

/// <summary>RCAS002 — รายงานการครบกำหนดทบทวนหลักประกันตามประเภท (collateral review-due by type).</summary>
internal static class Rcas002Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReviewType", "ReviewTypeCode", "RemainingDays", "AppraisalNumber", "ValuationDate", "PastDueDay"
    };

    // FSD sort sequence: "Review Type, Remaining Day". Sort by the raw code so the order is 1/2/3,
    // not the alphabetical order of the resolved labels.
    private static readonly string[] DefaultSort = ["ReviewTypeCode", "RemainingDays"];

    public static readonly ReportDefinition<Rcas002Row, Rcas002Filter> Definition = new()
    {
        BaseName = "RCAS002-ReappraisalDue",
        Title = "รายงานการครบกำหนดทบทวนหลักประกันตามประเภท",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, DefaultSort),
        Build = Build,
        DescribeFilter = f =>
        [
            // ReviewType is a fixed AS400 enum resolved in the view; mirror that mapping here.
            new("Review Type", MapReviewType(f.ReviewType)),
            new("Stage", f.Stage),
            new("Customer Name", f.CustomerName),
            new("Appraisal No.", f.AppraisalNumber),
            new("Created From", f.CreatedFrom?.ToString("yyyy-MM-dd")),
            new("Created To", f.CreatedTo?.ToString("yyyy-MM-dd")),
        ],
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

    // Mirrors the view's CASE: AS400 review code 1/2/3 -> readable label (multi-value safe).
    private static string? MapReviewType(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;

        var labels = csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code switch
            {
                "1" => "Normal",
                "2" => "Before Stage 3",
                "3" => "Stage 3",
                _ => code,
            });
        return string.Join(", ", labels);
    }

    private static (string, DynamicParameters) Build(Rcas002Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        // Bind the raw code (ReviewTypeCode); the ReviewType column is the resolved label, so binding
        // codes against it would never match.
        ReportFilterSql.MultiValue(c, p, f.ReviewType, "ReviewTypeCode", "ReviewTypes");
        ReportFilterSql.MultiValue(c, p, f.Stage, "Stage", "Stages");
        ReportFilterSql.Contains(c, p, f.CustomerName, "CustomerName", "CustomerName");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");
        // FSD "Create Date from … to". RCAS002 has no appraisal-create timestamp (AS400 reappraisal
        // candidates), so this binds ValuationDate (วันที่ประเมิน) as the closest date.
        // ⚑ CONFIRM with business which candidate date "Create Date" means, then repoint this column.
        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "ValuationDate", "Created");

        return ("SELECT * FROM reporting.vw_RCAS002_ReappraisalDue" + ReportFilterSql.Where(c), p);
    }
}
