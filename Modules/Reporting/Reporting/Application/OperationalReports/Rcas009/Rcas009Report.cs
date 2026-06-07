using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas009;

public sealed record Rcas009Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? PayType,
    string? AppraisalCompany,
    string? FeeStatus,
    string? SortBy,
    string? SortDir);

/// <summary>RCAS009 — รายงานสรุปค่าประเมิน (appraisal-fee summary to Accounting).</summary>
internal static class Rcas009Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "AppraisalCreateDate", "AppraisalCompany", "AppraisalFee"
    };

    public static readonly ReportDefinition<Rcas009Row, Rcas009Filter> Definition = new()
    {
        BaseName = "RCAS009-FeeSummary",
        Title = "รายงานสรุปค่าประเมิน",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "AppraisalNumber"),
        Build = Build,
        Columns =
        [
            new("Appraisal No.", r => r.AppraisalNumber),
            new("Customer Name", r => r.CustomerName),
            new("Assign Type", r => r.AssignType),
            new("Pay Type", r => r.PayType),
            new("Purpose", r => r.Purpose),
            new("Appraisal Create Date", r => r.AppraisalCreateDate, ColumnFormat.Date),
            new("Collateral Type", r => r.CollateralType),
            new("Status", r => r.AppraisalStatus),
            new("Requestor", r => r.RequestorCode),
            new("Requestor Dept.", r => r.RequestorDepartment),
            new("Retail/IBG", r => r.BankingSegment),
            new("Appraisal Company", r => r.AppraisalCompany),
            new("Internal Staff", r => r.InternalAppraisalStaff),
            new("Invoice No.", r => r.InvoiceNumber),
            new("Cost Center", r => r.CostCenter),
            new("Appraisal Fee", r => r.AppraisalFee, ColumnFormat.Money, Total: true),
            new("VAT", r => r.VAT, ColumnFormat.Money, Total: true),
            new("Include VAT", r => r.IncludeVAT, ColumnFormat.Money, Total: true),
            new("Fee Status", r => r.FeeStatus),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas009Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "AppraisalCreateDate", "Created");
        ReportFilterSql.MultiValue(c, p, f.PayType, "PayType", "PayTypes");
        ReportFilterSql.Contains(c, p, f.AppraisalCompany, "AppraisalCompany", "AppraisalCompany");
        ReportFilterSql.MultiValue(c, p, f.FeeStatus, "FeeStatus", "FeeStatuses");

        return ("SELECT * FROM reporting.vw_RCAS009_FeeSummary" + ReportFilterSql.Where(c), p);
    }
}
