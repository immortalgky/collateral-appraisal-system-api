using Dapper;

namespace Auth.Application.Features.Reports.GetUserAccessMatrix;

public class GetUserAccessMatrixQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetUserAccessMatrixQuery, GetUserAccessMatrixResult>
{
    public async Task<GetUserAccessMatrixResult> Handle(
        GetUserAccessMatrixQuery request,
        CancellationToken cancellationToken)
    {
        var (where, parameters) = UserAccessMatrixSqlBuilder.BuildFilter(
            request.Scope,
            request.CompanyId,
            request.RoleName,
            request.GroupId,
            request.TeamId,
            request.IsActive,
            request.Search);

        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var (countSql, dataSql) = UserAccessMatrixSqlBuilder.BuildPaginatedSql(
            where,
            request.PageNumber,
            pageSize);

        var connection = sqlConnectionFactory.GetOpenConnection();

        using var multi = await connection.QueryMultipleAsync(
            countSql + "; " + dataSql,
            parameters);

        var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<UserAccessRow>()).ToList();

        return new GetUserAccessMatrixResult(items, totalCount, request.PageNumber, pageSize);
    }
}
