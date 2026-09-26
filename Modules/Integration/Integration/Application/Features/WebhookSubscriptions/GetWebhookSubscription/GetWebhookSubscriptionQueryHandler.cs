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
        const string sql = $"""
            SELECT
            {WebhookSubscriptionSql.Columns}
            FROM integration.WebhookSubscriptions s
            WHERE s.Id = @Id
            """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        var dto = await connection.QuerySingleOrDefaultAsync<WebhookSubscriptionDto>(
            sql, new { request.Id });

        return dto ?? throw new NotFoundException("WebhookSubscription", request.Id);
    }
}
