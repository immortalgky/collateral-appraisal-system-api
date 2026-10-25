using System.Data;
using Dapper;

namespace Integration.Application.Features.WebhookDeliveries;

/// <summary>
/// The WHERE conditions shared by the delivery list and its summary, so the status-tab counts and the
/// rows under the tabs always filter identically. Callers join <c>integration.WebhookDeliveries d</c> to
/// <c>integration.WebhookSubscriptions s</c>. Every value is bound as a parameter; the condition strings
/// are fixed text.
/// </summary>
public static class WebhookDeliveryFilter
{
    public static List<string> Conditions(
        DynamicParameters parameters,
        Guid? subscriptionId,
        string? systemCode,
        string? eventType,
        string? search,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var conditions = new List<string>();

        if (subscriptionId.HasValue)
        {
            conditions.Add("d.SubscriptionId = @SubscriptionId");
            parameters.Add("SubscriptionId", subscriptionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(systemCode))
        {
            conditions.Add("s.SystemCode = @SystemCode");
            parameters.Add("SystemCode", systemCode);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            conditions.Add("d.EventType = @EventType");
            parameters.Add("EventType", eventType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // Escape LIKE wildcards so "%" or "_" typed by the user match literally.
            var like = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
            parameters.Add("SearchLike", like);

            if (Guid.TryParse(term, out var id))
            {
                conditions.Add("(d.EventType LIKE @SearchLike ESCAPE '\\' OR d.Id = @SearchId)");
                parameters.Add("SearchId", id);
            }
            else
            {
                conditions.Add("d.EventType LIKE @SearchLike ESCAPE '\\'");
            }
        }

        if (fromDate.HasValue)
        {
            conditions.Add("d.CreatedAt >= @FromDate");
            // DateTime2, like the column: a plain DateTime binds as SqlDbType.DateTime (3.33 ms rounding).
            parameters.Add("FromDate", fromDate.Value, DbType.DateTime2);
        }

        if (toDate.HasValue)
        {
            conditions.Add("d.CreatedAt <= @ToDate");
            parameters.Add("ToDate", toDate.Value, DbType.DateTime2);
        }

        return conditions;
    }

    public static string Where(IEnumerable<string> conditions)
    {
        var joined = string.Join(" AND ", conditions);
        return joined.Length > 0 ? "WHERE " + joined : "";
    }
}
