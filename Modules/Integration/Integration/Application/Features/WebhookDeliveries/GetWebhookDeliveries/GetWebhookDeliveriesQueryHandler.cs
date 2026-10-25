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
        var parameters = new DynamicParameters();

        var conditions = WebhookDeliveryFilter.Conditions(
            parameters, request.SubscriptionId, request.SystemCode, request.EventType, request.Search, request.FromDate, request.ToDate);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            conditions.Add("d.Status = @Status");
            parameters.Add("Status", request.Status);
        }

        var where = WebhookDeliveryFilter.Where(conditions);

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
            "d.CreatedAt DESC, d.Id DESC",
            paginationRequest,
            parameters);
    }
}
