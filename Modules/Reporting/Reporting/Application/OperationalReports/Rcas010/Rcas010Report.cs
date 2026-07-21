using Dapper;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Application.OperationalReports.Rcas010;

public sealed record Rcas010Filter(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string? Channel,
    string? DepartmentCode,
    string? AoCode,
    string? Status,
    string? FeeType,
    string? AppraisalCompany,
    string? SortBy,
    string? SortDir);

/// <summary>
/// RCAS010 — ค่าใช้จ่ายค่าประเมินที่ธนาคารจ่าย ประจำเดือน. A single SUMMARY row: Internal vs External
/// appraisal, each split Total / Customer-Paid / Bank-Absorb (book count + fee), plus a Grand Total.
/// The filters narrow the base rows BEFORE the aggregate, so the one row reflects the current
/// selection (e.g. bank-absorbed fees for all channels, or only Retail).
/// </summary>
internal static class Rcas010Report
{
    public static readonly ReportDefinition<Rcas010Row, Rcas010Filter> Definition = new()
    {
        BaseName = "RCAS010-FeeExpenseMonthly",
        Title = "ค่าใช้จ่ายค่าประเมินที่ธนาคารจ่าย ประจำเดือน",
        // Single row — sort is moot; order by a stable aggregate alias so the runner's ORDER BY binds.
        OrderBy = _ => "GrandTotalCount",
        Build = Build,
        DescribeFilter = f =>
        [
            new("Created From", f.CreatedFrom?.ToString("yyyy-MM-dd")),
            new("Created To", f.CreatedTo?.ToString("yyyy-MM-dd")),
            new("Channel", f.Channel, "Channel"),
            new("Department Code", f.DepartmentCode),
            new("AO Code", f.AoCode),
            new("Status", f.Status),
            new("Fee Type", f.FeeType, "FeePaymentMethod"),
            new("Appraisal Company", f.AppraisalCompany),
        ],
        Columns =
        [
            new("Internal – Books", r => r.InternalBookCount, ColumnFormat.Integer),
            new("Internal – Fee", r => r.InternalTotalFee, ColumnFormat.Money),
            new("Internal – Customer-Paid Books", r => r.InternalCustomerPaidCount, ColumnFormat.Integer),
            new("Internal – Customer-Paid Fee", r => r.InternalCustomerPaidFee, ColumnFormat.Money),
            new("Internal – Bank-Absorb Books", r => r.InternalBankAbsorbCount, ColumnFormat.Integer),
            new("Internal – Bank-Absorb Fee", r => r.InternalBankAbsorbFee, ColumnFormat.Money),
            new("External – Books", r => r.ExternalBookCount, ColumnFormat.Integer),
            new("External – Fee", r => r.ExternalTotalFee, ColumnFormat.Money),
            new("External – Customer-Paid Books", r => r.ExternalCustomerPaidCount, ColumnFormat.Integer),
            new("External – Customer-Paid Fee", r => r.ExternalCustomerPaidFee, ColumnFormat.Money),
            new("External – Bank-Absorb Books", r => r.ExternalBankAbsorbCount, ColumnFormat.Integer),
            new("External – Bank-Absorb Fee", r => r.ExternalBankAbsorbFee, ColumnFormat.Money),
            new("Grand Total – Books", r => r.GrandTotalCount, ColumnFormat.Integer),
            new("Grand Total – Fee", r => r.GrandTotalFee, ColumnFormat.Money),
        ],
    };

    private static (string, DynamicParameters) Build(Rcas010Filter f)
    {
        var c = new List<string>();
        var p = new DynamicParameters();

        ReportFilterSql.DateRange(c, p, f.CreatedFrom, f.CreatedTo, "CreatedAt", "Created");
        ReportFilterSql.MultiValue(c, p, f.Channel, "Channel", "Channels");
        ReportFilterSql.Exact(c, p, f.DepartmentCode, "RequestorDepartment", "DepartmentCode");
        ReportFilterSql.Exact(c, p, f.AoCode, "RequestorAoCode", "AoCode");
        ReportFilterSql.MultiValue(c, p, f.Status, "AppraisalStatus", "Statuses");
        ReportFilterSql.MultiValue(c, p, f.FeeType, "FeePaymentType", "FeeTypes");
        ReportFilterSql.Contains(c, p, f.AppraisalCompany, "AppraisalCompany", "AppraisalCompany");

        // Single-row aggregate. Internal/External are column groups (conditional aggregates), not row
        // dimensions. Counts are distinct appraisal books; fees are per-fee sums. Column order matches
        // the positional Rcas010Row.
        const string agg =
            "SELECT " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'Internal' THEN AppraisalId END) AS InternalBookCount, " +
            "SUM(CASE WHEN AssignType = 'Internal' THEN TotalFeeAfterVAT ELSE 0 END) AS InternalTotalFee, " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'Internal' AND CustomerPayableAmount > 0 THEN AppraisalId END) AS InternalCustomerPaidCount, " +
            "SUM(CASE WHEN AssignType = 'Internal' THEN CustomerPayableAmount ELSE 0 END) AS InternalCustomerPaidFee, " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'Internal' AND BankAbsorbAmount > 0 THEN AppraisalId END) AS InternalBankAbsorbCount, " +
            "SUM(CASE WHEN AssignType = 'Internal' THEN BankAbsorbAmount ELSE 0 END) AS InternalBankAbsorbFee, " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'External' THEN AppraisalId END) AS ExternalBookCount, " +
            "SUM(CASE WHEN AssignType = 'External' THEN TotalFeeAfterVAT ELSE 0 END) AS ExternalTotalFee, " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'External' AND CustomerPayableAmount > 0 THEN AppraisalId END) AS ExternalCustomerPaidCount, " +
            "SUM(CASE WHEN AssignType = 'External' THEN CustomerPayableAmount ELSE 0 END) AS ExternalCustomerPaidFee, " +
            "COUNT(DISTINCT CASE WHEN AssignType = 'External' AND BankAbsorbAmount > 0 THEN AppraisalId END) AS ExternalBankAbsorbCount, " +
            "SUM(CASE WHEN AssignType = 'External' THEN BankAbsorbAmount ELSE 0 END) AS ExternalBankAbsorbFee, " +
            "COUNT(DISTINCT AppraisalId) AS GrandTotalCount, " +
            "SUM(TotalFeeAfterVAT) AS GrandTotalFee " +
            "FROM reporting.vw_RCAS010_FeeExpenseBase";

        return ($"SELECT * FROM ({agg}{ReportFilterSql.Where(c)}) g", p);
    }
}
