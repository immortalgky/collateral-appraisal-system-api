using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Logs.SearchLogs;

public class SearchLogsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<SearchLogsQuery, PaginatedResult<LogDto>>
{
    public async Task<PaginatedResult<LogDto>> Handle(
        SearchLogsQuery query,
        CancellationToken cancellationToken)
    {
        // Explicit projection — column order MUST match LogDto's positional record constructor.
        var sql = @"
SELECT
    Id,
    TimeStamp,
    Level,
    Message,
    Exception,
    CorrelationId,
    EntityId,
    AppraisalId,
    RequestId,
    WorkflowInstanceId,
    CollateralId,
    DocumentId,
    MachineName
FROM dbo.Logs";

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

        if (!string.IsNullOrWhiteSpace(filter.Level))
        {
            conditions.Add("Level = @Level");
            parameters.Add("Level", filter.Level.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            conditions.Add("CorrelationId = @CorrelationId");
            parameters.Add("CorrelationId", filter.CorrelationId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalId))
        {
            conditions.Add("AppraisalId = @AppraisalId");
            parameters.Add("AppraisalId", filter.AppraisalId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.RequestId))
        {
            conditions.Add("RequestId = @RequestId");
            parameters.Add("RequestId", filter.RequestId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            conditions.Add("EntityId = @EntityId");
            parameters.Add("EntityId", filter.EntityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkflowInstanceId))
        {
            conditions.Add("WorkflowInstanceId = @WorkflowInstanceId");
            parameters.Add("WorkflowInstanceId", filter.WorkflowInstanceId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.CollateralId))
        {
            conditions.Add("CollateralId = @CollateralId");
            parameters.Add("CollateralId", filter.CollateralId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentId))
        {
            conditions.Add("DocumentId = @DocumentId");
            parameters.Add("DocumentId", filter.DocumentId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("Message LIKE @Search ESCAPE '\\'");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        if (filter.From.HasValue)
        {
            conditions.Add("TimeStamp >= @From");
            parameters.Add("From", filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            // A date-only "To" (e.g. 2026-05-26) binds to midnight; treat it as inclusive of the
            // whole end day so logs during that day aren't silently excluded.
            var to = filter.To.Value.TimeOfDay == TimeSpan.Zero
                ? filter.To.Value.Date.AddDays(1).AddTicks(-1)
                : filter.To.Value;
            conditions.Add("TimeStamp <= @To");
            parameters.Add("To", to);
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"TimeStamp {sortDir}";

        return await connectionFactory.QueryPaginatedAsync<LogDto>(sql, orderBy, query.Paging, parameters);
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
