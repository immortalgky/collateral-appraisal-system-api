using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

/// <summary>
/// Returns vendor-scoped quotation invitations with per-company status derived from
/// appraisal.vw_MyCompanyInvitationList. Always scoped to caller's CompanyId.
/// If the caller has no CompanyId (non-ext-company user), returns empty result.
/// </summary>
public class GetMyInvitationsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetMyInvitationsQuery, GetMyInvitationsResult>
{
    // Explicit column list matches MyInvitationDto. CompanyId is used for scoping only.
    // Customer columns come from the CustomerApply join below.
    private const string SelectColumns = """
        SELECT v.Id, v.QuotationNumber, v.RequestDate, v.CutOffTime, v.RequestedBy,
               v.TotalAppraisals, v.CompanyId, v.ReceivedAt, v.TotalFeeAmount,
               v.QuotedAt, v.QuotedBy, v.CompanyStatus, v.HasSubmitted,
               cust.CustomerName, cust.CustomerCount, cust.CustomerNames
        """;

    // Distinct customer names for the RFQ, via QuotationRequestAppraisals → Appraisals →
    // RequestCustomers (correlated on v.Id — the same chain the customer search filter uses).
    // Representative name = MIN; count + ordered full list drive the "+N" indicator and tooltip.
    private const string CustomerApply = """
        OUTER APPLY (
            SELECT CustomerName = MIN(x.Name), CustomerCount = COUNT(*),
                   CustomerNames = STRING_AGG(x.Name, ', ') WITHIN GROUP (ORDER BY x.Name)
            FROM (SELECT DISTINCT rc.Name
                  FROM appraisal.QuotationRequestAppraisals qra
                  JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                  JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                  WHERE qra.QuotationRequestId = v.Id) x
        ) cust
        """;

    public async Task<GetMyInvitationsResult> Handle(
        GetMyInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var companyId = currentUser.CompanyId;
        if (!companyId.HasValue)
            return EmptyResult(query.PaginationRequest);

        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("CompanyId", companyId.Value);

        var sql = $"{SelectColumns} FROM appraisal.vw_MyCompanyInvitationList v {CustomerApply} WHERE v.CompanyId = @CompanyId";

        // Optional filter: CompanyStatus (multi-select → IN list; Dapper expands the array)
        if (query.Statuses is { Length: > 0 })
        {
            sql += " AND v.CompanyStatus IN @Statuses";
            dynamicParams.Add("Statuses", query.Statuses);
        }

        // Optional per-field search. Each field is an INDEPENDENT predicate AND-ed onto the
        // query, so combining them NARROWS the result set. Each is a contains match ("%term%").
        // Mirrors GetQuotationsQueryHandler so both quotation listings expose the same fields.
        // v.Id is the QuotationRequest id, so appraisal/customer are reached via the same joins
        // the internal listing uses (QuotationRequestAppraisals → Appraisals → RequestCustomers).
        if (!string.IsNullOrWhiteSpace(query.QuotationNo))
        {
            sql += " AND v.QuotationNumber LIKE @QuotationNoPattern";
            dynamicParams.Add("QuotationNoPattern", BuildLikePattern(query.QuotationNo));
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalNo))
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                     WHERE qra.QuotationRequestId = v.Id
                       AND a.AppraisalNumber LIKE @AppraisalNoPattern
                 )
                """;
            dynamicParams.Add("AppraisalNoPattern", BuildLikePattern(query.AppraisalNo));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
                     JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                     WHERE qra.QuotationRequestId = v.Id
                       AND rc.Name LIKE @CustomerNamePattern
                 )
                """;
            dynamicParams.Add("CustomerNamePattern", BuildLikePattern(query.CustomerName));
        }

        // Optional filter: CutOffTime range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (query.CutOffTimeFrom.HasValue)
        {
            sql += " AND v.CutOffTime >= @CutOffTimeFrom";
            dynamicParams.Add("CutOffTimeFrom", query.CutOffTimeFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.CutOffTimeTo.HasValue)
        {
            sql += " AND v.CutOffTime <= @CutOffTimeTo";
            dynamicParams.Add("CutOffTimeTo", query.CutOffTimeTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        var result = await connectionFactory.QueryPaginatedAsync<MyInvitationDto>(
            sql,
            BuildOrderBy(query.SortBy, query.SortDir),
            query.PaginationRequest,
            dynamicParams);

        return new GetMyInvitationsResult(result);
    }

    // Whitelist of user-sortable columns → real view columns. Only these literal strings ever
    // reach the ORDER BY, so user input can never inject. Keys are what the frontend sends.
    private static readonly Dictionary<string, string> AllowedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QuotationNumber"] = "QuotationNumber",
        ["CompanyStatus"] = "CompanyStatus",
        ["TotalAppraisals"] = "TotalAppraisals",
        ["TotalFeeAmount"] = "TotalFeeAmount",
        ["ReceivedAt"] = "ReceivedAt",
        ["CutOffTime"] = "CutOffTime",
        ["QuotedAt"] = "QuotedAt",
        ["CustomerName"] = "cust.CustomerName",
    };

    // Default RequestDate DESC; Id is a stable tiebreaker (never a sort key → no duplicate-column trap).
    private static string BuildOrderBy(string? sortBy, string? sortDir)
    {
        var column = AllowedSortColumns.TryGetValue(sortBy ?? "", out var col) ? col : "RequestDate";
        var direction = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return $"{column} {direction}, Id DESC";
    }

    private static GetMyInvitationsResult EmptyResult(PaginationRequest request) =>
        new(new PaginatedResult<MyInvitationDto>([], 0, request.PageNumber, request.PageSize));

    // Contains match ("%term%") — matches anywhere in the field. On these listings this is
    // cheap: quotation number is a tiny table, and appraisal/customer ride a correlated EXISTS
    // driven from the small quotation set, so it never scans the large Appraisals /
    // RequestCustomers tables directly.
    private static string BuildLikePattern(string term) => $"%{term.Trim()}%";
}
