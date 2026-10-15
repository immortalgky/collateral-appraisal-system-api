using Dapper;
using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookSubscriptions.GetWebhookSubscriptions;

public record GetWebhookSubscriptionsQuery(
    int PageNumber, int PageSize, string? SystemCode, bool? IsActive, string? EventType = null)
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

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            conditions.Add("s.EventType = @EventType");
            parameters.Add("EventType", request.EventType);
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT
            {WebhookSubscriptionSql.Columns}
            FROM integration.WebhookSubscriptions s
            {where}
            """;

        var paginationRequest = new PaginationRequest(request.PageNumber - 1, request.PageSize);

        return await sqlConnectionFactory.QueryPaginatedAsync<WebhookSubscriptionDto>(
            sql,
            // Unique order: one SystemCode can have several rows (catch-all + per-event), and OFFSET/FETCH
            // over tied rows can repeat or skip them between pages.
            "s.SystemCode ASC, s.EventType ASC, s.Id ASC",
            paginationRequest,
            parameters);
    }
}
