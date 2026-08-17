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
            SELECT a.AppraisalNumber, a.Status, a.UpdatedAt, a.CreatedAt,
                   u.FirstName, u.LastName, u.Position, u.PhoneNumber
            FROM appraisal.Appraisals a
                OUTER APPLY (
                    SELECT pt.AssignedTo
                    FROM workflow.PendingTasks pt
                    WHERE pt.CorrelationId = a.RequestId
                    ORDER BY pt.AssignedAt DESC
                ) pt
                LEFT JOIN auth.AspNetUsers u
                       ON u.UserName = pt.AssignedTo
            WHERE a.AppraisalNumber = @appraisalNumber
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
        var picName = string.IsNullOrEmpty(row.FirstName) && string.IsNullOrEmpty(row.LastName)
            ? null
            : $"{row.FirstName} {row.LastName}".Trim();

        return new GetAppraisalStatusResponse(
            row.AppraisalNumber, status, lastUpdatedAt, picName, row.Position, row.PhoneNumber);
    }

    private record SqlAppraisalRow(
        string AppraisalNumber, string Status, DateTime? UpdatedAt, DateTime? CreatedAt,
        string? FirstName, string? LastName, string? Position, string? PhoneNumber);
}
