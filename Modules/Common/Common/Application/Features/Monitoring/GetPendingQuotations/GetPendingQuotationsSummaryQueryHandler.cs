using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

/// <summary>
/// Returns a Total count for the Pending Quotations monitoring tab.
/// appraisal.vw_QuotationList does not expose OlaVarianceHours/OlaTargetHours,
/// so Breached/AtRisk/Healthy bucket fields are null — only Total is returned.
/// The same terminal-status exclusion as the list handler is applied by default.
/// </summary>
public class GetPendingQuotationsSummaryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingQuotationsSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetPendingQuotationsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;
        if (filter.Status is { Length: > 0 })
        {
            conditions.Add("Status IN @Statuses");
            parameters.Add("Statuses", filter.Status);
        }
        else
        {
            conditions.Add("Status NOT IN ('Closed', 'Finalized', 'Cancelled')");
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(QuotationNumber LIKE @Search ESCAPE '\\' OR RequestedBy LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        var where = "WHERE " + string.Join(" AND ", conditions);
        var sql = $"SELECT COUNT(*) FROM appraisal.vw_QuotationList {where}";

        var conn = connectionFactory.GetOpenConnection();
        var total = await conn.ExecuteScalarAsync<int>(sql, parameters);
        return new MonitoringSummaryDto(total, null, null, null);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
