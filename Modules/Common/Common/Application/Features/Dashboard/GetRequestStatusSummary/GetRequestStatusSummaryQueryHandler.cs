using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Dashboard.GetRequestStatusSummary;

public class GetRequestStatusSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetRequestStatusSummaryQuery, GetRequestStatusSummaryResult>
{
    public async Task<GetRequestStatusSummaryResult> Handle(
        GetRequestStatusSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        var items = await connection.QueryAsync<RequestStatusDto>("""
            SELECT Status, Count
            FROM common.RequestStatusSummaries
            ORDER BY Status
            """);

        return new GetRequestStatusSummaryResult(items.ToList());
    }
}
