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
        parameters.Add("CurrentUsername", currentUserService.Username);

        const string sql = """
            WITH CurrentUserTeam AS (
                SELECT tm.TeamId
                FROM auth.TeamMembers tm
                INNER JOIN auth.AspNetUsers u ON u.Id = tm.UserId
                WHERE u.UserName = @CurrentUsername
            ),
            TeamUsers AS (
                -- Internal team: all members sharing a TeamId with the current user
                SELECT u.UserName
                FROM auth.TeamMembers tm
                INNER JOIN auth.AspNetUsers u ON u.Id = tm.UserId
                WHERE tm.TeamId IN (SELECT TeamId FROM CurrentUserTeam)
                UNION
                -- Same-company fallback: external users matched by CompanyId
                SELECT u.UserName
                FROM auth.AspNetUsers u
                INNER JOIN auth.AspNetUsers cu ON cu.UserName = @CurrentUsername
                WHERE u.CompanyId IS NOT NULL AND u.CompanyId = cu.CompanyId
            )
            SELECT
                v.Username,
                COUNT(CASE WHEN v.Bucket = 'NotStarted' THEN 1 END) AS NotStarted,
                COUNT(CASE WHEN v.Bucket = 'InProgress'  THEN 1 END) AS InProgress,
                COUNT(CASE WHEN v.Bucket = 'Completed'   THEN 1 END) AS Completed
            FROM workflow.vw_UserTaskSummary v
            INNER JOIN TeamUsers tu ON tu.UserName = v.Username
            GROUP BY v.Username
            ORDER BY (COUNT(CASE WHEN v.Bucket = 'NotStarted' THEN 1 END)
                    + COUNT(CASE WHEN v.Bucket = 'InProgress'  THEN 1 END)
                    + COUNT(CASE WHEN v.Bucket = 'Completed'   THEN 1 END)) DESC;
            """;

        var items = await connection.QueryAsync<TeamWorkloadDto>(sql, parameters);

        return new GetTeamWorkloadResult(items.ToList());
    }
}
