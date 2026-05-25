using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Integration.Application.Features.Appraisals.GetAppraisalStatus;

public class GetAppraisalStatusQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetAppraisalStatusQuery, GetAppraisalStatusResponse?>
{
    public async Task<GetAppraisalStatusResponse?> Handle(
        GetAppraisalStatusQuery query,
        CancellationToken cancellationToken)
    {
        using var conn = connectionFactory.GetOpenConnection();

        const string sql = """
            SELECT AppraisalNumber, Status, UpdatedAt, CreatedAt
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

        var lastUpdatedAt = row.UpdatedAt ?? row.CreatedAt ?? dateTimeProvider.ApplicationNow;
        return new GetAppraisalStatusResponse(row.AppraisalNumber, status, lastUpdatedAt);
    }

    private record SqlAppraisalRow(string AppraisalNumber, string Status, DateTime? UpdatedAt, DateTime? CreatedAt);
}
