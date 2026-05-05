using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.Parameters.GetParameters;

public class GetParametersQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetParametersQuery, List<ParameterGroupResult>>
{
    public async Task<List<ParameterGroupResult>> Handle(
        GetParametersQuery query,
        CancellationToken cancellationToken)
    {
        using var conn = connectionFactory.GetOpenConnection();

        var hasFilter = query.Groups is { Count: > 0 };

        var sql = hasFilter
            ? """
              SELECT [Group], Code, Description
              FROM parameter.Parameters
              WHERE IsActive = 1 AND [Group] IN @groups
              ORDER BY [Group], SeqNo
              """
            : """
              SELECT [Group], Code, Description
              FROM parameter.Parameters
              WHERE IsActive = 1
              ORDER BY [Group], SeqNo
              """;

        var rows = await conn.QueryAsync<SqlParameterRow>(
            new CommandDefinition(sql,
                hasFilter ? new { groups = query.Groups } : null,
                cancellationToken: cancellationToken));

        return rows
            .GroupBy(r => r.Group)
            .Select(g => new ParameterGroupResult(
                g.Key,
                g.Select(r => new ParameterValueItem(r.Code, r.Description ?? string.Empty)).ToList()))
            .ToList();
    }

    private record SqlParameterRow(string Group, string Code, string? Description);
}
