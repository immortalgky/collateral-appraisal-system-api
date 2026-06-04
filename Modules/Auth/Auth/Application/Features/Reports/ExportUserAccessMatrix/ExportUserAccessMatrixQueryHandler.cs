using Dapper;
using Auth.Application.Features.Reports.GetUserAccessMatrix;

namespace Auth.Application.Features.Reports.ExportUserAccessMatrix;

public class ExportUserAccessMatrixQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<ExportUserAccessMatrixQuery, ExportUserAccessMatrixResult>
{
    private static readonly string[] CsvHeaders =
    [
        "UserId", "UserName", "FullName", "Email",
        "CompanyName", "Scope", "IsActive", "Roles", "Groups", "Teams"
    ];

    public async Task<ExportUserAccessMatrixResult> Handle(
        ExportUserAccessMatrixQuery request,
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

        var sql = UserAccessMatrixSqlBuilder.BuildExportSql(where);

        var connection = sqlConnectionFactory.GetOpenConnection();

        var rows = (await connection.QueryAsync<UserAccessRow>(sql, parameters)).ToList();

        var csvBytes = CsvWriter.Write(
            CsvHeaders,
            rows.Select(r => new string?[]
            {
                r.UserId.ToString(),
                r.UserName,
                r.FullName,
                r.Email,
                r.CompanyName,
                r.Scope,
                r.IsActive.ToString(),
                r.Roles,
                r.Groups,
                r.Teams
            }));

        var fileName = $"user-access-report-{DateTime.UtcNow:yyyyMMdd}.csv";

        return new ExportUserAccessMatrixResult(csvBytes, fileName);
    }
}
