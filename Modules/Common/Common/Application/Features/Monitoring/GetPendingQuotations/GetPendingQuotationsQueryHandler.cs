using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

/// <summary>
/// Returns pending quotations from appraisal.vw_QuotationList.
/// No activity-ID scoping — this is an admin-level screen with a single permission.
/// Customer names are derived inline (OUTER APPLY) and the RM code is resolved to a full name
/// via a LEFT JOIN to auth.AspNetUsers — mirrors the quotation listing (GetQuotationsQueryHandler).
/// </summary>
public class GetPendingQuotationsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingQuotationsQuery, PaginatedResult<PendingQuotationDto>>
{
    // Whitelist of user-sortable keys → real (qualified) columns. Only these literal strings ever
    // reach the ORDER BY, so user input can never inject.
    private static readonly Dictionary<string, string> AllowedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QuotationNumber"] = "q.QuotationNumber",
        ["Status"] = "q.Status",
        ["RequestDate"] = "q.RequestDate",
        ["CutOffTime"] = "q.CutOffTime",
        ["RequestedBy"] = "q.RequestedBy",
        ["RmUsername"] = "q.RmUsername",
        ["TotalAppraisals"] = "q.TotalAppraisals",
        ["TotalCompaniesInvited"] = "q.TotalCompaniesInvited",
        ["TotalQuotationsReceived"] = "q.TotalQuotationsReceived",
        ["CustomerName"] = "cust.CustomerName",
    };

    public async Task<PaginatedResult<PendingQuotationDto>> Handle(
        GetPendingQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        // Column order MUST match PendingQuotationDto's positional record constructor.
        // - RmFullName: NULL when the code doesn't resolve to a real name → the UI shows just the code.
        // - cust.*: distinct customer names for the quotation (representative MIN + count + full list).
        var sql = @"
SELECT
    q.Id,
    q.QuotationNumber,
    q.Status,
    q.RequestDate,
    q.CutOffTime,
    q.RequestedBy,
    q.TotalAppraisals,
    q.TotalCompaniesInvited,
    q.TotalQuotationsReceived,
    q.RmUsername,
    RmFullName = NULLIF(LTRIM(RTRIM(COALESCE(rmU.FirstName, '') + ' ' + COALESCE(rmU.LastName, ''))), ''),
    cust.CustomerName,
    cust.CustomerCount,
    cust.CustomerNames
FROM appraisal.vw_QuotationList q
LEFT JOIN auth.AspNetUsers rmU ON rmU.UserName = q.RmUsername
OUTER APPLY (
    SELECT CustomerName = MIN(x.Name), CustomerCount = COUNT(*),
           CustomerNames = STRING_AGG(x.Name, ', ') WITHIN GROUP (ORDER BY x.Name)
    FROM (SELECT DISTINCT rc.Name
          FROM appraisal.QuotationRequestAppraisals qra
          JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
          JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
          WHERE qra.QuotationRequestId = q.Id) x
) cust";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

        // Default: exclude terminal statuses to show only pending/open quotations.
        // If specific statuses are requested, filter exactly (multi-select IN).
        if (filter.Status is { Length: > 0 })
        {
            conditions.Add("q.Status IN @Statuses");
            parameters.Add("Statuses", filter.Status);
        }
        else
        {
            conditions.Add("q.Status NOT IN ('Closed', 'Finalized', 'Cancelled')");
        }

        // Per-field search — each an INDEPENDENT contains predicate AND-ed onto the query
        // (mirrors the quotation listing). Appraisal-no / customer-name reach through the
        // QuotationRequestAppraisals → Appraisals → RequestCustomers chain.
        if (!string.IsNullOrWhiteSpace(filter.QuotationNo))
        {
            conditions.Add(@"q.QuotationNumber LIKE @QuotationNoPattern ESCAPE '\'");
            parameters.Add("QuotationNoPattern", "%" + EscapeLike(filter.QuotationNo.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalNo))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
    JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
    WHERE qra.QuotationRequestId = q.Id
      AND a.AppraisalNumber LIKE @AppraisalNoPattern ESCAPE '\')");
            parameters.Add("AppraisalNoPattern", "%" + EscapeLike(filter.AppraisalNo.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
    JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
    JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
    WHERE qra.QuotationRequestId = q.Id
      AND rc.Name LIKE @CustomerNamePattern ESCAPE '\')");
            parameters.Add("CustomerNamePattern", "%" + EscapeLike(filter.CustomerName.Trim()) + "%");
        }

        // Optional filter: CutOffTime range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (filter.CutOffTimeFrom.HasValue)
        {
            conditions.Add("q.CutOffTime >= @CutOffTimeFrom");
            parameters.Add("CutOffTimeFrom", filter.CutOffTimeFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (filter.CutOffTimeTo.HasValue)
        {
            conditions.Add("q.CutOffTime <= @CutOffTimeTo");
            parameters.Add("CutOffTimeTo", filter.CutOffTimeTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // Optional filter: quotations that invited a specific appraisal company.
        if (!string.IsNullOrWhiteSpace(filter.AppraisalCompanyId))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationInvitations qi
    WHERE qi.QuotationRequestId = q.Id
      AND qi.CompanyId = @AppraisalCompanyId)");
            parameters.Add("AppraisalCompanyId", filter.AppraisalCompanyId);
        }

        sql += " WHERE " + string.Join(" AND ", conditions);

        // Default RequestDate DESC; q.Id is a stable tiebreaker (never a sort key → no duplicate trap).
        var sortColumn = AllowedSortColumns.TryGetValue(filter.SortBy ?? "", out var col) ? col : "q.RequestDate";
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortColumn} {sortDir}, q.Id DESC";

        return await connectionFactory.QueryPaginatedAsync<PendingQuotationDto>(sql, orderBy, query.Paging, parameters);
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
