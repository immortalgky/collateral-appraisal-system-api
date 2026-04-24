using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Appraisal.Application.Features.Quotations.GetQuotations;

/// <summary>
/// Handler for getting Quotations with pagination.
/// Results are scoped by caller role:
///   Admin / IntAdmin : all quotations.
///   RM               : only quotations where RmUserId = current user.
///   ExtAdmin         : only quotations the caller's company is invited to.
///   Anyone else      : empty result.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetQuotationsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetQuotationsQuery, GetQuotationsResult>
{
    // Explicit column list matches the QuotationDto positional record constructor exactly.
    // RmUserId and AppraisalId are used for filtering only — not selected into the DTO.
    private const string SelectColumns = """
        SELECT q.Id, q.QuotationNumber, q.RequestDate, q.DueDate, q.Status, q.RequestedByName,
               q.TotalAppraisals, q.TotalCompaniesInvited, q.TotalQuotationsReceived
        """;

    public async Task<GetQuotationsResult> Handle(
        GetQuotationsQuery query,
        CancellationToken cancellationToken)
    {
        string sql;
        var dynamicParams = new DynamicParameters();

        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
        {
            sql = $"{SelectColumns} FROM appraisal.vw_QuotationList q WHERE 1 = 1";
        }
        else if (currentUser.IsInRole("RequestMaker"))
        {
            sql = $"{SelectColumns} FROM appraisal.vw_QuotationList q WHERE q.RmUserId = @UserId";
            dynamicParams.Add("UserId", currentUser.UserId);
        }
        else if (currentUser.IsInRole("ExtAdmin"))
        {
            var companyId = currentUser.CompanyId;
            if (!companyId.HasValue)
                return EmptyResult(query.PaginationRequest);

            sql = $$"""
                {{SelectColumns}} FROM appraisal.vw_QuotationList q
                WHERE EXISTS (
                    SELECT 1 FROM appraisal.QuotationInvitations qi
                    WHERE qi.QuotationRequestId = q.Id
                      AND qi.CompanyId = @CompanyId
                )
                """;
            dynamicParams.Add("CompanyId", companyId.Value);
        }
        else
        {
            return EmptyResult(query.PaginationRequest);
        }

        // v2: filter by appraisal via the join table (AppraisalId column dropped)
        if (query.AppraisalId.HasValue)
        {
            sql += """
                 AND EXISTS (
                     SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
                     WHERE qra.QuotationRequestId = q.Id
                       AND qra.AppraisalId = @AppraisalId
                 )
                """;
            dynamicParams.Add("AppraisalId", query.AppraisalId.Value);
        }

        var result = await connectionFactory.QueryPaginatedAsync<QuotationDto>(
            sql,
            "RequestDate DESC",
            query.PaginationRequest,
            dynamicParams);

        return new GetQuotationsResult(result);
    }

    private static GetQuotationsResult EmptyResult(PaginationRequest request) =>
        new(new PaginatedResult<QuotationDto>([], 0, request.PageNumber, request.PageSize));
}
