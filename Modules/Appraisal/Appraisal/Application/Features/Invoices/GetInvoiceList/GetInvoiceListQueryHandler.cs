using Dapper;

namespace Appraisal.Application.Features.Invoices.GetInvoiceList;

public class GetInvoiceListQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetInvoiceListQuery, PaginatedResult<InvoiceListDto>>
{
    public async Task<PaginatedResult<InvoiceListDto>> Handle(
        GetInvoiceListQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(request.Status))
        {
            conditions.Add("Status = @Status");
            parameters.Add("Status", request.Status);
        }
        if (!string.IsNullOrEmpty(request.CompanySearch))
        {
            conditions.Add("CompanyName LIKE @CompanySearch");
            parameters.Add("CompanySearch", $"%{request.CompanySearch}%");
        }
        if (request.CompanyId.HasValue)
        {
            conditions.Add("CompanyId = @CompanyId");
            parameters.Add("CompanyId", request.CompanyId.Value);
        }
        if (request.DateFrom.HasValue)
        {
            conditions.Add("CreatedAt >= @DateFrom");
            parameters.Add("DateFrom", request.DateFrom.Value);
        }
        if (request.DateTo.HasValue)
        {
            conditions.Add("CreatedAt <= @DateTo");
            parameters.Add("DateTo", request.DateTo.Value);
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var sql = $"SELECT * FROM appraisal.vw_InvoiceList {where}";

        var paginationRequest = new PaginationRequest(request.PageNumber, request.PageSize);
        return await sqlConnectionFactory.QueryPaginatedAsync<InvoiceListDto>(
            sql,
            "CreatedAt DESC",
            paginationRequest,
            parameters);
    }
}
