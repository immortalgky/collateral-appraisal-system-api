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

        // Optional date range — applied to EventAt (AssignedAt for active/overdue,
        // CompletedAt for completed rows) as exposed by the view.
        var dateConditions = new List<string>();

        if (query.From.HasValue)
        {
            dateConditions.Add("v.EventAt >= @From");
            parameters.Add("From", query.From.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.To.HasValue)
        {
            dateConditions.Add("v.EventAt < DATEADD(day, 1, @To)");
            parameters.Add("To", query.To.Value.ToDateTime(TimeOnly.MinValue));
        }

        var dateWhere = dateConditions.Count > 0
            ? "AND " + string.Join(" AND ", dateConditions)
            : string.Empty;

        var sql = $"""
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
                COUNT(CASE WHEN v.Bucket = 'Completed'   THEN 1 END) AS Completed,
                COUNT(CASE WHEN v.Bucket = 'Overdue'     THEN 1 END) AS Overdue
            FROM workflow.vw_UserTaskSummary v
            INNER JOIN TeamUsers tu ON tu.UserName = v.Username
            WHERE 1 = 1 {dateWhere}
            GROUP BY v.Username
            ORDER BY (COUNT(CASE WHEN v.Bucket = 'NotStarted' THEN 1 END)
                    + COUNT(CASE WHEN v.Bucket = 'InProgress'  THEN 1 END)
                    + COUNT(CASE WHEN v.Bucket = 'Completed'   THEN 1 END)) DESC;
            """;

        var items = await connection.QueryAsync<TeamWorkloadDto>(sql, parameters);

        return new GetTeamWorkloadResult(items.ToList());
    }
}
