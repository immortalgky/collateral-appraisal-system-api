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
        "AppraisalNumber", "AppraisalCompany", "InternalAppraisalStaff", "ApprovedDate", "BankingSegmentName",
        "TotalScorePct", "ScoreReportQuality", "RemarkReportQuality", "ScoreDeliveryTime", "RemarkDeliveryTime",
        "ScorePersonnel", "RemarkPersonnel", "ScoreResponseTime", "RemarkResponseTime",
        "ScoreCoordination", "RemarkCoordination", "Remark", "Note",
    };

    // FSD sort sequence: "Appraisal company, Verify date". "Verify date" is taken as ApprovedDate
    // (a.CompletedAt) — ⚑ confirm with business whether verify date differs from approved date.
    private static readonly string[] DefaultSort = ["AppraisalCompany", "ApprovedDate"];

    public static readonly ReportDefinition<Rcas008Row, Rcas008Filter> Definition = new()
    {
        BaseName = "RCAS008-ServiceQuality",
        Title = "รายงานประเมินผลคุณภาพการให้บริการของ External Appraisal Company แต่ละบริษัท",
        OrderBy = OrderBy,
        Build = Build,
        DescribeFilter = f =>
        [
            new("Approved From", f.ApprovedFrom?.ToString("yyyy-MM-dd"), LabelTh: "วันที่อนุมัติ ตั้งแต่"),
            new("Approved To", f.ApprovedTo?.ToString("yyyy-MM-dd"), LabelTh: "วันที่อนุมัติ ถึง"),
            new("Banking Segment", f.BankingSegment, "BankingSegment", LabelTh: "กลุ่มลูกค้า"),
            new("Appraisal Company", f.AppraisalCompany, LabelTh: "บริษัทประเมิน"),
            new("Evaluation Status", f.EvaluationStatus, "EvaluationStatus", LabelTh: "สถานะการให้คะแนน"),
            new("Appraisal No.", f.AppraisalNumber, LabelTh: "หมายเลขเล่มประเมิน"),
            new("Purpose", f.Purpose, "AppraisalPurpose", LabelTh: "วัตถุประสงค์"),
            new("Appraisal Type", f.AppraisalType, LabelTh: "ประเภทการประเมิน"),
        ],
        Columns =
        [
            new("Appraisal No.", r => r.AppraisalNumber, HeaderTh: "หมายเลขเล่มประเมิน", FontColor: "#0d9488", Total: true),
            new("Appraisal Company", r => r.AppraisalCompany, HeaderTh: "บริษัทประเมิน"),
            new("Internal Staff", r => r.InternalAppraisalStaff, HeaderTh: "ผู้ประเมินภายใน"),
            new("Approved Date", r => r.ApprovedDate, ColumnFormat.Date, HeaderTh: "วันที่อนุมัติ", Center: true),
            new("Banking Segment", r => r.BankingSegmentName ?? r.BankingSegment, HeaderTh: "กลุ่มลูกค้า", Center: true),
            new("Total Score %", r => r.TotalScorePct, ColumnFormat.Percent, HeaderTh: "คะแนนรวม", Center: true),
            // Score labels follow the FSD "Detail of Field" wording verbatim (items 7-15).
            new("Score of Report book quality", r => r.ScoreReportQuality, ColumnFormat.Integer, HeaderTh: "คุณภาพเล่มรายงาน", Center: true),
            new("Remark of Report book quality", r => r.RemarkReportQuality, HeaderTh: "หมายเหตุ"),
            new("Score of Delivery time (SLA)", r => r.ScoreDeliveryTime, ColumnFormat.Integer, HeaderTh: "คะแนนระยะเวลา", Center: true),
            new("Remark of Delivery time (SLA)", r => r.RemarkDeliveryTime, HeaderTh: "หมายเหตุ"),
            new("Score of Preparing the company's personnel for accepting bank assessment work",
                r => r.ScorePersonnel, ColumnFormat.Integer, HeaderTh: "การจัดเตรียมบุคลากร", Center: true),
            new("Remark of Preparing the company's personnel for accepting bank assessment work",
                r => r.RemarkPersonnel, HeaderTh: "หมายเหตุ"),
            new("Score of Response time to problem resolution", r => r.ScoreResponseTime, ColumnFormat.Integer, HeaderTh: "ระยะเวลาการตอบสนอง", Center: true),
            new("Remark of Response time to problem resolution", r => r.RemarkResponseTime, HeaderTh: "หมายเหตุ"),
            new("Score of Coordination and responsibility in work", r => r.ScoreCoordination, ColumnFormat.Integer, HeaderTh: "การประสานงาน", Center: true),
            new("Remark of Coordination and responsibility in work", r => r.RemarkCoordination, HeaderTh: "หมายเหตุ"),
            new("Additional Comments from the Evaluators", r => r.Remark, HeaderTh: "ความคิดเห็นเพิ่มเติมจากผู้ประเมิน"),
            new("Note", r => r.Note, HeaderTh: "บันทึก"),
        ],
    };

    // Deterministic tiebreaker so OFFSET paging can't duplicate or skip rows: AppraisalNumber (unless it is
    // already the primary sort), then Id (unique per row). Kept here, not in the shared helper.
    private static string OrderBy(Rcas008Filter f)
    {
        var primary = ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, DefaultSort);
        var sortsByNumber = primary.StartsWith("AppraisalNumber ", StringComparison.OrdinalIgnoreCase);
        return primary + (sortsByNumber ? ", Id ASC" : ", AppraisalNumber ASC, Id ASC");
    }

    private static (string, DynamicParameters) Build(Rcas008Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.ApprovedFrom, f.ApprovedTo, "ApprovedDate", "Approved");
        ReportFilterSql.MultiValue(c, p, f.BankingSegment, "BankingSegment", "BankingSegments");
        ReportFilterSql.ContainsAny(c, p, f.AppraisalCompany, ["AppraisalCompany", "AppraisalCompanyEn"], "AppraisalCompany");
        ReportFilterSql.MultiValue(c, p, f.EvaluationStatus, "EvaluationStatus", "EvaluationStatuses");
        ReportFilterSql.Contains(c, p, f.AppraisalNumber, "AppraisalNumber", "AppraisalNumber");
        // Purpose binds the raw code (PurposeCode); Appraisal type binds its stored code.
        ReportFilterSql.MultiValue(c, p, f.Purpose, "PurposeCode", "Purposes");
        ReportFilterSql.MultiValue(c, p, f.AppraisalType, "AppraisalType", "AppraisalTypes");

        return ("SELECT * FROM reporting.vw_RCAS008_ServiceQuality" + ReportFilterSql.Where(c), p);
    }
}
