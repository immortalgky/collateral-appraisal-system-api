using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

/// <summary>
/// Handler for getting all Committees with pagination.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetCommitteesQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetCommitteesQuery, GetCommitteesResult>
{
    public async Task<GetCommitteesResult> Handle(
        GetCommitteesQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM appraisal.vw_CommitteeList";

        var result = await connectionFactory.QueryPaginatedAsync<CommitteeDto>(
            sql,
            "CreatedAt DESC",
            query.PaginationRequest);

        return new GetCommitteesResult(result);
    }
}
