using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetEligibleAppraisalsForQuotation;

/// <summary>
/// Handles the GetEligibleAppraisalsForQuotationQuery.
/// Reuses AppraisalFilterBuilder and appends two hard eligibility predicates:
///   1. AssignmentStatus IS NULL — no active (non-rejected/cancelled) assignment.
///   2. NOT EXISTS against QuotationRequestAppraisals where the quotation is non-terminal.
/// </summary>
public class GetEligibleAppraisalsForQuotationQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetEligibleAppraisalsForQuotationQuery, PaginatedResult<AppraisalDto>>
{
    // "Not yet assigned" = no latest active assignment row, OR latest assignment is still Pending
    // (queued but not handed out). The view's LEFT JOIN already filters Rejected/Cancelled out, so
    // AssignmentStatus seen here is one of NULL, Pending, Assigned, InProgress, Completed.
    private const string EligibilityClause = """
        (v.AssignmentStatus IS NULL OR v.AssignmentStatus = 'Pending')
        AND NOT EXISTS (
            SELECT 1
            FROM appraisal.QuotationRequestAppraisals qa
            JOIN appraisal.QuotationRequests qr ON qr.Id = qa.QuotationRequestId
            WHERE qa.AppraisalId = v.Id
              AND qr.Status NOT IN ('Finalized', 'Cancelled')
        )
        """;

    public async Task<PaginatedResult<AppraisalDto>> Handle(
        GetEligibleAppraisalsForQuotationQuery query,
        CancellationToken cancellationToken)
    {
        var (whereClause, parameters) = AppraisalFilterBuilder.BuildFilter(query.Filter);
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(query.Filter);

        var combinedWhere = string.IsNullOrEmpty(whereClause)
            ? $" WHERE {EligibilityClause}"
            : $"{whereClause} AND ({EligibilityClause})";

        var baseSql = "SELECT v.* FROM appraisal.vw_AppraisalList v" + combinedWhere;

        return await connectionFactory.QueryPaginatedAsync<AppraisalDto>(
            baseSql,
            orderBy,
            query.PaginationRequest,
            parameters);
    }
}
