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

        // Query real appraisal statuses directly. Map "Assigned" → "InProgress"
        // since the dashboard uses a 5-bucket model without a separate Assigned state.
        var items = await connection.QueryAsync<AppraisalStatusDto>("""
            SELECT
                CASE Status
                    WHEN 'Assigned' THEN 'InProgress'
                    ELSE Status
                END AS Status,
                COUNT(*) AS Count
            FROM appraisal.Appraisals
            WHERE Status IN ('Pending', 'Assigned', 'InProgress', 'UnderReview', 'Completed', 'Cancelled')
            GROUP BY
                CASE Status
                    WHEN 'Assigned' THEN 'InProgress'
                    ELSE Status
                END
            """);

        return new GetAppraisalStatusSummaryResult(items.ToList());
    }
}
