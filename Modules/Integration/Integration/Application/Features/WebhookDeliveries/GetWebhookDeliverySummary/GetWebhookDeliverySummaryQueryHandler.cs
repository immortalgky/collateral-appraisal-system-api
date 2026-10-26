using Dapper;
using Integration.Domain.WebhookDeliveries;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliverySummary;

public class GetWebhookDeliverySummaryQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetWebhookDeliverySummaryQuery, WebhookDeliverySummaryDto>
{
    private const int TopCauses = 3;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The WHERE text comes from WebhookDeliveryFilter, which only emits fixed condition strings with bound " +
            "@parameters, plus the DeliveryStatus constants — never caller input.")]
    public async Task<WebhookDeliverySummaryDto> Handle(
        GetWebhookDeliverySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = WebhookDeliveryFilter.Conditions(
            parameters, request.SubscriptionId, request.SystemCode, request.EventType, request.Search, request.FromDate, request.ToDate);

        var all = WebhookDeliveryFilter.Where(conditions);
        var pending = WebhookDeliveryFilter.Where(conditions.Append($"d.Status = '{DeliveryStatus.Pending}'"));
        var failed = WebhookDeliveryFilter.Where(conditions.Append($"d.Status = '{DeliveryStatus.Failed}'"));

        const string from = """
            FROM integration.WebhookDeliveries d
            INNER JOIN integration.WebhookSubscriptions s ON s.Id = d.SubscriptionId
            """;

        // One round trip; the tab counts and the breakdowns share the list's filter.
        var sql = $"""
            SELECT
                COUNT(*) AS Total,
                COALESCE(SUM(CASE WHEN d.Status = '{DeliveryStatus.Pending}' THEN 1 ELSE 0 END), 0) AS Pending,
                COALESCE(SUM(CASE WHEN d.Status = '{DeliveryStatus.Delivered}' THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN d.Status = '{DeliveryStatus.Failed}' THEN 1 ELSE 0 END), 0) AS Failed,
                MIN(CASE WHEN d.Status = '{DeliveryStatus.Pending}' THEN d.CreatedAt END) AS OldestPendingAt,
                COALESCE(SUM(CASE WHEN d.Status = '{DeliveryStatus.Pending}' AND d.AttemptCount = 0 THEN 1 ELSE 0 END), 0) AS NeverAttemptedCount
            {from}
            {all};

            SELECT s.SystemCode, COUNT(*) AS Count
            {from}
            {pending}
            GROUP BY s.SystemCode
            ORDER BY COUNT(*) DESC, s.SystemCode;

            SELECT d.EventType, COUNT(*) AS Count
            {from}
            {failed}
            GROUP BY d.EventType
            ORDER BY COUNT(*) DESC, d.EventType;

            SELECT s.SystemCode, d.LastStatusCode, s.HttpMethod, s.CallbackUrl, COUNT(*) AS Count
            {from}
            {failed}
            GROUP BY s.SystemCode, d.LastStatusCode, s.HttpMethod, s.CallbackUrl
            ORDER BY COUNT(*) DESC, s.SystemCode, s.HttpMethod, s.CallbackUrl;
            """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var counts = await multi.ReadFirstAsync<CountsRow>();
        var bySystem = (await multi.ReadAsync<SystemCountDto>()).ToList();
        var failedByEventType = (await multi.ReadAsync<EventTypeCountDto>()).ToList();
        // Grouped by the display URL, not the stored one, so two URLs differing only in a hidden query string
        // are one cause; hence the top-N is taken here rather than with TOP in SQL.
        var failedByCause = (await multi.ReadAsync<FailureCauseDto>())
            .Select(c => c with { CallbackUrl = WebhookCallbackUrl.ToDisplay(c.CallbackUrl) })
            .GroupBy(c => (c.SystemCode, c.LastStatusCode, c.HttpMethod, c.CallbackUrl), CauseKeyComparer.Instance)
            .Select(g => g.First() with { Count = g.Sum(c => c.Count) })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.SystemCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.LastStatusCode)
            .ThenBy(c => c.HttpMethod, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.CallbackUrl, StringComparer.OrdinalIgnoreCase)
            .Take(TopCauses)
            .ToList();

        return new WebhookDeliverySummaryDto(
            counts.Total,
            counts.Pending,
            counts.Delivered,
            counts.Failed,
            counts.Pending > 0
                ? new OldestPendingDto(counts.OldestPendingAt, counts.NeverAttemptedCount, bySystem)
                : null,
            failedByEventType,
            failedByCause);
    }

    // The columns compare case-insensitively in SQL, so the merge after display-URL sanitising must too.
    private sealed class CauseKeyComparer
        : IEqualityComparer<(string SystemCode, int? LastStatusCode, string HttpMethod, string CallbackUrl)>
    {
        public static readonly CauseKeyComparer Instance = new();

        public bool Equals(
            (string SystemCode, int? LastStatusCode, string HttpMethod, string CallbackUrl) x,
            (string SystemCode, int? LastStatusCode, string HttpMethod, string CallbackUrl) y) =>
            x.LastStatusCode == y.LastStatusCode
            && string.Equals(x.SystemCode, y.SystemCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.HttpMethod, y.HttpMethod, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.CallbackUrl, y.CallbackUrl, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string SystemCode, int? LastStatusCode, string HttpMethod, string CallbackUrl) k) =>
            HashCode.Combine(
                k.LastStatusCode,
                StringComparer.OrdinalIgnoreCase.GetHashCode(k.SystemCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(k.HttpMethod),
                StringComparer.OrdinalIgnoreCase.GetHashCode(k.CallbackUrl));
    }

    private sealed record CountsRow(
        int Total, int Pending, int Delivered, int Failed, DateTime? OldestPendingAt, int NeverAttemptedCount);
}
