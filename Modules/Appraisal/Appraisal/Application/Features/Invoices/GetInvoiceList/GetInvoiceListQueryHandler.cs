using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public class GetInvoiceListQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetInvoiceListQuery, InvoiceListResult>
{
    public async Task<InvoiceListResult> Handle(
        GetInvoiceListQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // External (company) callers see only their company's invoices; the request.CompanyId is
        // ignored to prevent cross-company peeking. Bank callers fall through to the optional
        // request.CompanyId block below.
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);
        if (enforcedCompanyId.HasValue)
        {
            conditions.Add("CompanyId = @ScopedCompanyId");
            parameters.Add("ScopedCompanyId", enforcedCompanyId.Value);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            conditions.Add("Status = @Status");
            parameters.Add("Status", request.Status);
        }

        if (!string.IsNullOrEmpty(request.ExcludeStatus))
        {
            conditions.Add("Status <> @ExcludeStatus");
            parameters.Add("ExcludeStatus", request.ExcludeStatus);
        }

        if (!string.IsNullOrEmpty(request.CompanySearch))
        {
            conditions.Add("CompanyName LIKE @CompanySearch");
            parameters.Add("CompanySearch", $"%{request.CompanySearch}%");
        }

        if (!enforcedCompanyId.HasValue && request.CompanyId.HasValue)
        {
            conditions.Add("CompanyId = @CompanyId");
            parameters.Add("CompanyId", request.CompanyId.Value);
        }

        if (request.SentDateFrom.HasValue)
        {
            conditions.Add("SentDate >= @SentDateFrom");
            parameters.Add("SentDateFrom", request.SentDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (request.SentDateTo.HasValue)
        {
            conditions.Add("SentDate <= @SentDateTo");
            parameters.Add("SentDateTo", request.SentDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        if (request.PaidDateFrom.HasValue)
        {
            conditions.Add("PaidDate >= @PaidDateFrom");
            parameters.Add("PaidDateFrom", request.PaidDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (request.PaidDateTo.HasValue)
        {
            conditions.Add("PaidDate <= @PaidDateTo");
            parameters.Add("PaidDateTo", request.PaidDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            // Single search bar matches either the invoice number or any line-item customer name.
            // Use an uncorrelated `IN (subquery)` so we don't fight outer-vs-inner Id resolution.
            conditions.Add(
                "(InvoiceNumber LIKE @Search " +
                "OR Id IN (SELECT ii.InvoiceId FROM appraisal.InvoiceItems ii WHERE ii.CustomerName LIKE @Search))");
            parameters.Add("Search", $"%{request.Search}%");
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        const string listColumns = """
            Id, InvoiceNumber, Status, TotalAmount, ItemCount,
            PeriodStartDate, PeriodEndDate, SubmittedAt, ApprovedAt, ApprovedBy,
            PaymentOrderNo, PaidDate, SentDate, CompanyId, CompanyName, CreatedAt
            """;
        var aggregateSql = $"SELECT COUNT(Id) AS GrandItemCount, ISNULL(SUM(TotalAmount), 0) AS GrandTotalAmount FROM appraisal.vw_InvoiceList {where}";
        // Group-by clustering is always the leading key so rows stay grouped under
        // their cluster header. Sorting (when specified) is applied within the cluster
        // and falls back to CreatedAt DESC for stable ordering.
        var groupClause = request.GroupBy?.ToLowerInvariant() switch
        {
            "company" => "CompanyName",
            _ => null,
        };

        // Whitelist sortable columns to defeat SQL injection — the value comes from
        // a query string. Anything unknown falls through to the default.
        var sortColumn = request.SortBy?.ToLowerInvariant() switch
        {
            "invoicenumber" => "InvoiceNumber",
            "companyname" => "CompanyName",
            "sentdate" => "SentDate",
            "paiddate" => "PaidDate",
            "totalamount" => "TotalAmount",
            "totalitems" => "ItemCount",
            "status" => "Status",
            _ => null,
        };
        var sortDir = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";

        string orderBy;
        if (sortColumn is not null)
            orderBy = groupClause is not null
                ? $"{groupClause}, {sortColumn} {sortDir}, CreatedAt DESC"
                : $"{sortColumn} {sortDir}, CreatedAt DESC";
        else
            orderBy = groupClause is not null
                ? $"{groupClause}, CreatedAt DESC"
                : "CreatedAt DESC";
        var dataSql = $"SELECT {listColumns} FROM appraisal.vw_InvoiceList {where} ORDER BY {orderBy} OFFSET {request.PageNumber * request.PageSize} ROWS FETCH NEXT {request.PageSize} ROWS ONLY";
        var countSql = $"SELECT COUNT(Id) FROM appraisal.vw_InvoiceList {where}";

        var connection = sqlConnectionFactory.GetOpenConnection();

        using var multi = await connection.QueryMultipleAsync(
            aggregateSql + "; " + countSql + "; " + dataSql,
            parameters);

        var aggregate = await multi.ReadFirstOrDefaultAsync<AggregateRow>();
        var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<InvoiceListDto>()).ToList();

        return new InvoiceListResult(
            items,
            totalCount,
            request.PageNumber,
            request.PageSize,
            aggregate?.GrandItemCount ?? 0,
            aggregate?.GrandTotalAmount ?? 0m);
    }

    private record AggregateRow(int GrandItemCount, decimal GrandTotalAmount);
}
