using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.Appraisals.GetAppraisalStatus;

public class GetAppraisalStatusQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppraisalStatusQuery, GetAppraisalStatusResponse?>
{
    public async Task<GetAppraisalStatusResponse?> Handle(
        GetAppraisalStatusQuery query,
        CancellationToken cancellationToken)
    {
        using var conn = connectionFactory.GetOpenConnection();

        const string sql = """
            SELECT AppraisalNumber, Status, UpdatedOn 
            FROM appraisal.Appraisals
            WHERE AppraisalNumber = @appraisalNumber
            """;

        var row = await conn.QuerySingleOrDefaultAsync<SqlAppraisalRow>(
            new CommandDefinition(sql,
                new { appraisalNumber = query.AppraisalNumber },
                cancellationToken: cancellationToken));

        if (row is null) return null;

        var status = row.Status?.Trim() switch
        {
            var s when string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase) => "COMPLETED",
            var s when string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase) => "CANCELLED",
            _ => "IN_PROGRESS"
        };

        return new GetAppraisalStatusResponse(row.AppraisalNumber, status, row.UpdatedOn);
    }

    private record SqlAppraisalRow(string AppraisalNumber, string Status, DateTime UpdatedOn);
}
