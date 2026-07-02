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
    string? SortBy,
    string? SortDir);

/// <summary>RCAS008 — รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company.</summary>
internal static class Rcas008Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalCompany", "ApprovedDate", "AppraisalNumber", "TotalScorePct"
    };

    public static readonly ReportDefinition<Rcas008Row, Rcas008Filter> Definition = new()
    {
        BaseName = "RCAS008-ServiceQuality",
        Title = "รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company แต่ละบริษัท",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalCompany"),
        Build = Build,
        DescribeFilter = f =>
        [
            new("Approved From", f.ApprovedFrom?.ToString("yyyy-MM-dd")),
            new("Approved To", f.ApprovedTo?.ToString("yyyy-MM-dd")),
            new("Retail/IBG", f.BankingSegment, "BankingSegment"),
            new("Appraisal Company", f.AppraisalCompany),
            new("Evaluation Status", f.EvaluationStatus, "EvaluationStatus"),
            new("Appraisal No.", f.AppraisalNumber),
        ],
        Columns =
        [
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Approved Date", r => r.ApprovedDate, ColumnFormat.Date),
            new("Retail/IBG", r => r.BankingSegment),
            new("Total Score %", r => r.TotalScorePct, ColumnFormat.Percent),
            new("Report Quality", r => r.ScoreReportQuality, ColumnFormat.Integer),
            new("Delivery Time", r => r.ScoreDeliveryTime, ColumnFormat.Integer),
            new("Personnel", r => r.ScorePersonnel, ColumnFormat.Integer),
            new("Response Time", r => r.ScoreResponseTime, ColumnFormat.Integer),
            new("Coordination", r => r.ScoreCoordination, ColumnFormat.Integer),
            new("Remark", r => r.Remark),
            new("Evaluation Status", r => r.EvaluationStatus),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas008Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.ApprovedFrom, f.ApprovedTo, "ApprovedDate", "Approved");
        ReportFilterSql.Exact(c, p, f.BankingSegment, "BankingSegment", "BankingSegment");
        ReportFilterSql.Contains(c, p, f.AppraisalCompany, "AppraisalCompany", "AppraisalCompany");
        ReportFilterSql.MultiValue(c, p, f.EvaluationStatus, "EvaluationStatus", "EvaluationStatuses");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");

        return ("SELECT * FROM reporting.vw_RCAS008_ServiceQuality" + ReportFilterSql.Where(c), p);
    }
}
