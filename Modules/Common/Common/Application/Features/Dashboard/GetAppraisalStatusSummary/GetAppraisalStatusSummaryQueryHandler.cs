using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Dashboard.GetAppraisalStatusSummary;

public class GetAppraisalStatusSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalStatusSummaryQuery, GetAppraisalStatusSummaryResult>
{
    public async Task<GetAppraisalStatusSummaryResult> Handle(
        GetAppraisalStatusSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();

        // Build optional filter predicates
        var conditions = new List<string>
        {
            "a.Status IN ('Pending', 'Assigned', 'InProgress', 'UnderReview', 'Completed', 'Cancelled')",
            "a.IsDeleted = 0"
        };

        if (query.From.HasValue)
        {
            conditions.Add("a.CreatedAt >= @From");
            parameters.Add("From", query.From.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.To.HasValue)
        {
            conditions.Add("a.CreatedAt < DATEADD(day, 1, @To)");
            parameters.Add("To", query.To.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM appraisal.AppraisalAssignments aa
                    WHERE aa.AppraisalId = a.Id
                      AND aa.AssigneeUserId = @AssigneeId
                      AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                )
                """);
            parameters.Add("AssigneeId", query.AssigneeId);
        }

        if (!string.IsNullOrWhiteSpace(query.BankingSegment))
        {
            conditions.Add("a.BankingSegment = @BankingSegment");
            parameters.Add("BankingSegment", query.BankingSegment);
        }

        var whereClause = string.Join(" AND ", conditions);

        // Query real appraisal statuses directly. Map "Assigned" → "InProgress"
        // since the dashboard uses a 5-bucket model without a separate Assigned state.
        var sql = $"""
            SELECT
                CASE a.Status
                    WHEN 'Assigned' THEN 'InProgress'
                    ELSE a.Status
                END AS Status,
                COUNT(*) AS Count
            FROM appraisal.Appraisals a
            WHERE {whereClause}
            GROUP BY
                CASE a.Status
                    WHEN 'Assigned' THEN 'InProgress'
                    ELSE a.Status
                END
            """;

        var items = await connection.QueryAsync<AppraisalStatusDto>(sql, parameters);

        return new GetAppraisalStatusSummaryResult(items.ToList());
    }
}
