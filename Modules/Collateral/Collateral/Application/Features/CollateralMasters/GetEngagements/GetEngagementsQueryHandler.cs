using Dapper;

namespace Collateral.Application.Features.CollateralMasters.GetEngagements;

/// <summary>
/// Returns paginated engagement list (metadata only — no snapshot) from vw_CollateralEngagements.
/// Ordered by AppraisalDate DESC per spec.
/// </summary>
public class GetEngagementsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetEngagementsQuery, GetEngagementsResult>
{
    private const string SelectColumns = """
        SELECT
            Id,
            CollateralMasterId,
            AppraisalId,
            AppraisalNumber,
            RequestId,
            RequestNumber,
            PropertyId,
            AppraisalType,
            AppraisalDate,
            AppraisedValue,
            AppraiserUserId,
            AppraisalCompanyId,
            AppraisalCompanyName,
            CreatedOn,
            CollateralType,
            OwnerName
        """;

    public async Task<GetEngagementsResult> Handle(
        GetEngagementsQuery query,
        CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} FROM collateral.vw_CollateralEngagements WHERE CollateralMasterId = @MasterId";
        var p = new DynamicParameters();
        p.Add("MasterId", query.CollateralMasterId);

        var result = await connectionFactory.QueryPaginatedAsync<EngagementListItemDto>(
            sql,
            "AppraisalDate DESC",
            query.PaginationRequest,
            p);

        return new GetEngagementsResult(result);
    }
}
