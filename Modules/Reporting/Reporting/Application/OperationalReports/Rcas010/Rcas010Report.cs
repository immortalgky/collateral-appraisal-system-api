using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas010;

public sealed record Rcas010Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Channel,
    string? AssignType,
    string? SortBy,
    string? SortDir);

/// <summary>
/// RCAS010 — ค่าใช้จ่ายค่าประเมินที่ธนาคารจ่าย ประจำเดือน. Aggregated: the create-date filter is
/// applied to the row-level base view BEFORE the GROUP BY, so it lives in the report SQL.
/// </summary>
internal static class Rcas010Report
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "Channel", "AssignType", "BookCount", "TotalFee"
    };

    public static readonly ReportDefinition<Rcas010Row, Rcas010Filter> Definition = new()
    {
        BaseName = "RCAS010-FeeExpenseMonthly",
        Title = "ค่าใช้จ่ายค่าประเมินที่ธนาคารจ่าย ประจำเดือน",
        OrderBy = f => ReportFilterSql.OrderBy(f.SortBy, f.SortDir, AllowedSort, "Channel"),
        Build = Build,
        Columns =
        [
            new("Channel", r => r.Channel),
            new("Assign Type", r => r.AssignType),
            new("Book Count", r => r.BookCount, ColumnFormat.Integer, Total: true),
            new("Total Fee", r => r.TotalFee, ColumnFormat.Money, Total: true),
            new("Customer-Paid Count", r => r.CustomerPaidCount, ColumnFormat.Integer, Total: true),
            new("Customer-Paid Fee", r => r.CustomerPaidFee, ColumnFormat.Money, Total: true),
            new("Bank-Absorb Count", r => r.BankAbsorbCount, ColumnFormat.Integer, Total: true),
            new("Bank-Absorb Fee", r => r.BankAbsorbFee, ColumnFormat.Money, Total: true),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas010Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "CreatedAt", "Created");
        ReportFilterSql.Exact(c, p, f.Channel, "Channel", "Channel");
        ReportFilterSql.Exact(c, p, f.AssignType, "AssignType", "AssignType");

        var inner =
            // All three counts are distinct appraisal books (same grain); fee columns are per-fee sums.
            "SELECT Channel, AssignType, COUNT(DISTINCT AppraisalId) AS BookCount, SUM(TotalFeeAfterVAT) AS TotalFee, " +
            "COUNT(DISTINCT CASE WHEN CustomerPayableAmount > 0 THEN AppraisalId END) AS CustomerPaidCount, " +
            "SUM(CustomerPayableAmount) AS CustomerPaidFee, " +
            "COUNT(DISTINCT CASE WHEN BankAbsorbAmount > 0 THEN AppraisalId END) AS BankAbsorbCount, " +
            "SUM(BankAbsorbAmount) AS BankAbsorbFee " +
            "FROM reporting.vw_RCAS010_FeeExpenseBase" + ReportFilterSql.Where(c) +
            " GROUP BY Channel, AssignType";

        return ($"SELECT * FROM ({inner}) g", p);
    }
}
