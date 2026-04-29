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
    // Explicit column list matches MyInvitationDto positional record constructor exactly.
    // CompanyId is used for scoping only — it is still selected so Dapper maps the DTO.
    private const string SelectColumns = """
        SELECT v.Id, v.QuotationNumber, v.RequestDate, v.DueDate, v.RequestedBy,
               v.TotalAppraisals, v.CompanyId, v.ReceivedAt, v.TotalFeeAmount,
               v.QuotedAt, v.QuotedBy, v.CompanyStatus, v.HasSubmitted
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

        var sql = $"{SelectColumns} FROM appraisal.vw_MyCompanyInvitationList v WHERE v.CompanyId = @CompanyId";

        // Optional filter: CompanyStatus
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            sql += " AND v.CompanyStatus = @Status";
            dynamicParams.Add("Status", query.Status.Trim());
        }

        // Optional filter: free-text search across QuotationNumber and RequestedBy
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql += " AND (v.QuotationNumber LIKE @SearchPattern OR v.RequestedBy LIKE @SearchPattern)";
            dynamicParams.Add("SearchPattern", "%" + query.Search.Trim() + "%");
        }

        // Optional filter: DueDate range — convert DateOnly → DateTime (Dapper rejects DateOnly)
        if (query.DueDateFrom.HasValue)
        {
            sql += " AND v.DueDate >= @DueDateFrom";
            dynamicParams.Add("DueDateFrom", query.DueDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.DueDateTo.HasValue)
        {
            sql += " AND v.DueDate <= @DueDateTo";
            dynamicParams.Add("DueDateTo", query.DueDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        var result = await connectionFactory.QueryPaginatedAsync<MyInvitationDto>(
            sql,
            "RequestDate DESC",
            query.PaginationRequest,
            dynamicParams);

        return new GetMyInvitationsResult(result);
    }

    private static GetMyInvitationsResult EmptyResult(PaginationRequest request) =>
        new(new PaginatedResult<MyInvitationDto>([], 0, request.PageNumber, request.PageSize));
}
