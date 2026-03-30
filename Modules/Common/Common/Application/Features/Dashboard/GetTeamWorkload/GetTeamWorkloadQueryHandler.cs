using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetTeamWorkload;

public class GetTeamWorkloadQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetTeamWorkloadQuery, GetTeamWorkloadResult>
{
    public async Task<GetTeamWorkloadResult> Handle(
        GetTeamWorkloadQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();

        // If user has a company, scope to their company team
        var companyFilter = "";
        var companyId = currentUserService.CompanyId;
        if (companyId.HasValue)
        {
            companyFilter = "WHERE TeamId = @TeamId";
            parameters.Add("TeamId", companyId.Value.ToString());
        }

        var sql = $"""
            SELECT Username, NotStarted, InProgress, Completed
            FROM common.TeamWorkloadSummaries
            {companyFilter}
            ORDER BY (NotStarted + InProgress + Completed) DESC
            """;

        var items = await connection.QueryAsync<TeamWorkloadDto>(sql, parameters);

        return new GetTeamWorkloadResult(items.ToList());
    }
}
