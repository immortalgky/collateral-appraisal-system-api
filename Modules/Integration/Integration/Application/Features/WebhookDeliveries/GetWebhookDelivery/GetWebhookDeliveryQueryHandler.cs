using Dapper;
using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDelivery;

public class GetWebhookDeliveryQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetWebhookDeliveryQuery, WebhookDeliveryDetailDto?>
{
    public async Task<WebhookDeliveryDetailDto?> Handle(
        GetWebhookDeliveryQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.Id,
                d.SubscriptionId,
                s.SystemCode,
                d.EventType,
                d.Payload,
                d.Status,
                d.AttemptCount,
                d.LastStatusCode,
                d.LastError,
                d.DeliveredAt,
                d.CreatedAt
            FROM integration.WebhookDeliveries d
            INNER JOIN integration.WebhookSubscriptions s ON s.Id = d.SubscriptionId
            WHERE d.Id = @Id
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Id", request.Id);

        return await sqlConnectionFactory.QueryFirstOrDefaultAsync<WebhookDeliveryDetailDto>(sql, parameters);
    }
}
