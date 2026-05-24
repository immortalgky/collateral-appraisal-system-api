using Dapper;

namespace Appraisal.Application.Evaluations.Queries;

public record GetEvaluationListQuery(
    PaginationRequest PaginationRequest,
    string? Search                = null,
    string? AppraisalNumber       = null,
    string? CustomerName          = null,
    string? AppraisalStatus       = null,
    string? AppraiserName         = null,
    string? AppraiserCompanyId    = null,
    string? AppraiserCompanyName  = null,
    string? EvaluationStatus      = null,
    string? SortBy                = null,
    string? SortDir               = null
) : IQuery<GetEvaluationListResult>;

public record GetEvaluationListResult(PaginatedResult<AppraisalEvaluationListItem> Items);

public record AppraisalEvaluationListItem(
    Guid      AppraisalId,
    string?   AppraisalNumber,
    string?   CustomerName,
    DateTime? ReportReceivedDate,
    string?   AppraisalStatus,
    string?   ExternalAppraiserName,
    string?   AssigneeCompanyId,
    string?   AppraiserCompanyName,
    decimal?  AppraisalValue,
    Guid?     EvaluationId,
    string    EvaluationStatus,
    decimal?  TotalScore);

public class GetEvaluationListQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetEvaluationListQuery, GetEvaluationListResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "ReportReceivedDate", "AppraisalStatus",
        "AppraiserCompanyName", "AppraisalValue", "EvaluationStatus"
    };

    // Escape SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private const string BaseQuery =
        "SELECT AppraisalId, AppraisalNumber, CustomerName, ReportReceivedDate, " +
        "AppraisalStatus, ExternalAppraiserName, AssigneeCompanyId, AppraiserCompanyName, " +
        "AppraisalValue, EvaluationId, EvaluationStatus, TotalScore " +
        "FROM appraisal.vw_AppraisalEvaluationList " +
        "WHERE 1 = 1";

    public async Task<GetEvaluationListResult> Handle(
        GetEvaluationListQuery query,
        CancellationToken cancellationToken)
    {
        var sql = BaseQuery;
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql += " AND (AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\')";
            parameters.Add("Search", "%" + EscapeLike(query.Search.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalNumber))
        {
            sql += " AND AppraisalNumber LIKE @AppraisalNumber ESCAPE '\\'";
            parameters.Add("AppraisalNumber", "%" + EscapeLike(query.AppraisalNumber.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND CustomerName LIKE @CustomerName ESCAPE '\\'";
            parameters.Add("CustomerName", "%" + EscapeLike(query.CustomerName.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalStatus))
        {
            sql += " AND AppraisalStatus = @AppraisalStatus";
            parameters.Add("AppraisalStatus", query.AppraisalStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.AppraiserName))
        {
            sql += " AND ExternalAppraiserName LIKE @AppraiserName ESCAPE '\\'";
            parameters.Add("AppraiserName", "%" + EscapeLike(query.AppraiserName.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.AppraiserCompanyId))
        {
            sql += " AND AssigneeCompanyId = @AppraiserCompanyId";
            parameters.Add("AppraiserCompanyId", query.AppraiserCompanyId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.AppraiserCompanyName))
        {
            sql += " AND AppraiserCompanyName LIKE @AppraiserCompanyName ESCAPE '\\'";
            parameters.Add("AppraiserCompanyName", "%" + EscapeLike(query.AppraiserCompanyName.Trim()) + "%");
        }

        // Note: the view projects EvaluationStatus as COALESCE(e.EvaluationStatus, 'Pending'),
        // so filtering on 'Pending' returns both real Pending rows AND appraisals that have
        // no evaluation row yet. Treat 'Pending' as "awaiting evaluation" rather than a
        // distinct lifecycle state. If a future caller needs to distinguish "started but not
        // submitted" from "never touched", add an EvaluationId-null filter alongside this one.
        if (!string.IsNullOrWhiteSpace(query.EvaluationStatus))
        {
            var statuses = query.EvaluationStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (statuses.Length == 1)
            {
                sql += " AND EvaluationStatus = @EvaluationStatus";
                parameters.Add("EvaluationStatus", statuses[0]);
            }
            else if (statuses.Length > 1)
            {
                sql += " AND EvaluationStatus IN @EvaluationStatuses";
                parameters.Add("EvaluationStatuses", statuses);
            }
        }

        var orderBy = "AppraisalId DESC";
        if (!string.IsNullOrWhiteSpace(query.SortBy) && AllowedSortFields.Contains(query.SortBy))
        {
            var dir = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            orderBy = $"{query.SortBy} {dir}";
        }

        var result = await connectionFactory.QueryPaginatedAsync<AppraisalEvaluationListItem>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        return new GetEvaluationListResult(result);
    }
}
