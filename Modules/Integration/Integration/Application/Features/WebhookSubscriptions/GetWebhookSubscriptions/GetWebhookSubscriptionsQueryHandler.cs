using Dapper;
using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookSubscriptions;

public record GetWebhookSubscriptionsQuery(
    int PageNumber, int PageSize, string? SystemCode, bool? IsActive)
    : IQuery<PaginatedResult<WebhookSubscriptionDto>>;

public class GetWebhookSubscriptionsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetWebhookSubscriptionsQuery, PaginatedResult<WebhookSubscriptionDto>>
{
    public async Task<PaginatedResult<WebhookSubscriptionDto>> Handle(
        GetWebhookSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.SystemCode))
        {
            conditions.Add("s.SystemCode LIKE @SystemCode");
            parameters.Add("SystemCode", $"%{request.SystemCode}%");
        }

        if (request.IsActive.HasValue)
        {
            conditions.Add("s.IsActive = @IsActive");
            parameters.Add("IsActive", request.IsActive.Value);
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT
                s.Id,
                s.SystemCode,
                s.CallbackUrl,
                s.IsActive,
                RIGHT(s.SecretKey, 4) AS SecretLast4,
                s.LastDeliveryAt,
                s.CreatedAt
            FROM integration.WebhookSubscriptions s
            {where}
            """;

        var paginationRequest = new PaginationRequest(request.PageNumber - 1, request.PageSize);

        return await sqlConnectionFactory.QueryPaginatedAsync<WebhookSubscriptionDto>(
            sql,
            "s.SystemCode ASC",
            paginationRequest,
            parameters);
    }
}
