using Dapper;
using Shared.CQRS;
using Shared.Exceptions;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookSubscription;

public record GetWebhookSubscriptionQuery(Guid Id) : IQuery<WebhookSubscriptionDto>;

public class GetWebhookSubscriptionQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetWebhookSubscriptionQuery, WebhookSubscriptionDto>
{
    public async Task<WebhookSubscriptionDto> Handle(
        GetWebhookSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.Id,
                s.SystemCode,
                s.EventType,
                s.CallbackUrl,
                s.IsActive,
                RIGHT(s.SecretKey, 4) AS SecretLast4,
                s.LastDeliveryAt,
                s.CreatedAt
            FROM integration.WebhookSubscriptions s
            WHERE s.Id = @Id
            """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        var dto = await connection.QuerySingleOrDefaultAsync<WebhookSubscriptionDto>(sql, new { request.Id });

        return dto ?? throw new NotFoundException("WebhookSubscription", request.Id);
    }
}
