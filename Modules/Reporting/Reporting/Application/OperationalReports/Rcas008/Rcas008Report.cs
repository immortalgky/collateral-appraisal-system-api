using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas008;

public sealed record Rcas008Filter(
    DateTime? ApprovedFrom,
    DateTime? ApprovedTo,
    string? BankingSegment,
    string? AppraisalCompany,
    string? EvaluationStatus,
    string? AppraisalNumber,
    string? Purpose,
    string? AppraisalType,
    string? SortBy,
    string? SortDir);

/// <summary>RCAS008 — รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company.</summary>
internal static class Rcas008Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalCompany", "ApprovedDate", "AppraisalNumber", "TotalScorePct"
    };

    // FSD sort sequence: "Appraisal company, Verify date". "Verify date" is taken as ApprovedDate
    // (a.CompletedAt) — ⚑ confirm with business whether verify date differs from approved date.
    private static readonly string[] DefaultSort = ["AppraisalCompany", "ApprovedDate"];

    public static readonly ReportDefinition<Rcas008Row, Rcas008Filter> Definition = new()
    {
        BaseName = "RCAS008-ServiceQuality",
        Title = "รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company แต่ละบริษัท",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, DefaultSort),
        Build = Build,
        DescribeFilter = f =>
        [
            new("Approved From", f.ApprovedFrom?.ToString("yyyy-MM-dd")),
            new("Approved To", f.ApprovedTo?.ToString("yyyy-MM-dd")),
            new("Retail/IBG", f.BankingSegment, "BankingSegment"),
            new("Appraisal Company", f.AppraisalCompany),
            new("Evaluation Status", f.EvaluationStatus, "EvaluationStatus"),
            new("Appraisal No.", f.AppraisalNumber),
            new("Purpose", f.Purpose, "AppraisalPurpose"),
            new("Appraisal Type", f.AppraisalType),
        ],
        Columns =
        [
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Internal Staff", r => r.InternalAppraisalStaff),
            new("Approved Date", r => r.ApprovedDate, ColumnFormat.Date),
            new("Banking Segment", r => r.BankingSegment),
            new("Total Score %", r => r.TotalScorePct, ColumnFormat.Percent),
            // Score labels follow the FSD "Detail of Field" wording verbatim (items 7-15).
            new("Score of Report book quality", r => r.ScoreReportQuality, ColumnFormat.Integer),
            new("Score of Delivery time (SLA)", r => r.ScoreDeliveryTime, ColumnFormat.Integer),
            new("Score of Preparing the company's personnel for accepting bank assessment work",
                r => r.ScorePersonnel, ColumnFormat.Integer),
            new("Score of Response time to problem resolution", r => r.ScoreResponseTime, ColumnFormat.Integer),
            new("Score of Coordination and responsibility in work", r => r.ScoreCoordination, ColumnFormat.Integer),
            new("Remark", r => r.Remark),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas008Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.ApprovedFrom, f.ApprovedTo, "ApprovedDate", "Approved");
        ReportFilterSql.MultiValue(c, p, f.BankingSegment, "BankingSegment", "BankingSegments");
        ReportFilterSql.Contains(c, p, f.AppraisalCompany, "AppraisalCompany", "AppraisalCompany");
        ReportFilterSql.MultiValue(c, p, f.EvaluationStatus, "EvaluationStatus", "EvaluationStatuses");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");
        // Purpose binds the raw code (PurposeCode); Appraisal type binds its stored code.
        ReportFilterSql.MultiValue(c, p, f.Purpose, "PurposeCode", "Purposes");
        ReportFilterSql.MultiValue(c, p, f.AppraisalType, "AppraisalType", "AppraisalTypes");

        return ("SELECT * FROM reporting.vw_RCAS008_ServiceQuality" + ReportFilterSql.Where(c), p);
    }
}
