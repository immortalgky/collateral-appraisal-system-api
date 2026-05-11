using Dapper;
using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliveries;

public class GetWebhookDeliveriesQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetWebhookDeliveriesQuery, PaginatedResult<WebhookDeliveryListDto>>
{
    public async Task<PaginatedResult<WebhookDeliveryListDto>> Handle(
        GetWebhookDeliveriesQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            conditions.Add("d.Status = @Status");
            parameters.Add("Status", request.Status);
        }

        if (request.SubscriptionId.HasValue)
        {
            conditions.Add("d.SubscriptionId = @SubscriptionId");
            parameters.Add("SubscriptionId", request.SubscriptionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            conditions.Add("d.EventType = @EventType");
            parameters.Add("EventType", request.EventType);
        }

        if (request.FromDate.HasValue)
        {
            conditions.Add("d.CreatedAt >= @FromDate");
            parameters.Add("FromDate", request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            conditions.Add("d.CreatedAt <= @ToDate");
            parameters.Add("ToDate", request.ToDate.Value);
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        var sql = $"""
            SELECT
                d.Id,
                d.SubscriptionId,
                s.SystemCode,
                d.EventType,
                d.Status,
                d.AttemptCount,
                d.LastStatusCode,
                d.LastError,
                d.DeliveredAt,
                d.CreatedAt
            FROM integration.WebhookDeliveries d
            INNER JOIN integration.WebhookSubscriptions s ON s.Id = d.SubscriptionId
            {where}
            """;

        var paginationRequest = new PaginationRequest(request.PageNumber - 1, request.PageSize);

        return await sqlConnectionFactory.QueryPaginatedAsync<WebhookDeliveryListDto>(
            sql,
            "d.CreatedAt DESC",
            paginationRequest,
            parameters);
    }
}
