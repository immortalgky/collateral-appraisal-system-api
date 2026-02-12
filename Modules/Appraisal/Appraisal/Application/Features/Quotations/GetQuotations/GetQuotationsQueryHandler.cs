using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Quotations.GetQuotations;

/// <summary>
/// Handler for getting all Quotations with pagination.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetQuotationsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
{
    public async Task<GetQuotationsResult> Handle(
        GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM appraisal.vw_QuotationList";

        var result = await connectionFactory.QueryPaginatedAsync<QuotationDto>(
            sql,
            "RequestDate DESC",
            query.PaginationRequest);

        return new GetQuotationsResult(result);
    }
}
