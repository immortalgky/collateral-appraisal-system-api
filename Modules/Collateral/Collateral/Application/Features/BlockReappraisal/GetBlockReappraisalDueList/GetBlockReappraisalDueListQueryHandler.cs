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

        if (!string.IsNullOrWhiteSpace(query.ProjectName))
        {
            conditions.Add("ProjectName LIKE @ProjectName");
            parameters.Add("ProjectName", $"%{query.ProjectName.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.OldAppraisalNumber))
        {
            conditions.Add("OldAppraisalNumber LIKE @OldAppraisalNumber");
            parameters.Add("OldAppraisalNumber", $"%{query.OldAppraisalNumber.Trim()}%");
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
