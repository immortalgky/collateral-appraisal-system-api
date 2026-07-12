using Dapper;

namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDueList;

public class GetBlockReappraisalDueListQueryHandler(
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetBlockReappraisalDueListQuery, GetBlockReappraisalDueListResult>
{
    public async Task<GetBlockReappraisalDueListResult> Handle(
        GetBlockReappraisalDueListQuery query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(ProjectName LIKE @Search OR OldAppraisalNumber LIKE @Search)");
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        if (query.LastAppraisedDateFrom.HasValue)
        {
            conditions.Add("CAST(LastAppraisedDate AS date) >= @LastAppraisedDateFrom");
            parameters.Add("LastAppraisedDateFrom", query.LastAppraisedDateFrom.Value.Date);
        }

        if (query.LastAppraisedDateTo.HasValue)
        {
            conditions.Add("CAST(LastAppraisedDate AS date) <= @LastAppraisedDateTo");
            parameters.Add("LastAppraisedDateTo", query.LastAppraisedDateTo.Value.Date);
        }

        if (query.RemainingDayMin.HasValue)
        {
            conditions.Add("RemainingDay >= @RemainingDayMin");
            parameters.Add("RemainingDayMin", query.RemainingDayMin.Value);
        }

        if (query.RemainingDayMax.HasValue)
        {
            conditions.Add("RemainingDay <= @RemainingDayMax");
            parameters.Add("RemainingDayMax", query.RemainingDayMax.Value);
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        var sql = $"SELECT * FROM collateral.vw_BlockReappraisalDueList {where}";

        var result = await connectionFactory.QueryPaginatedAsync<BlockReappraisalDueListItem>(
            sql,
            "DueDate ASC",
            query.PaginationRequest,
            parameters);

        return new GetBlockReappraisalDueListResult(result);
    }
}
