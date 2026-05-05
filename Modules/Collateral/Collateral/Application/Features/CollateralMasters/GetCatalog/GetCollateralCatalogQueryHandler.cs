using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// Paginated catalog handler. Backed by vw_CollateralMasters via Dapper.
/// Admin-only: enforces IsInRole("Admin") || IsInRole("IntAdmin").
/// Supports type, province (Land/Condo), owner LIKE, isUnderConstruction (Land),
/// minAppraisals, lastAppraised date range, and sort whitelist.
/// </summary>
public class GetCollateralCatalogQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetCollateralCatalogQuery, GetCollateralCatalogResult>
{
    // Sort whitelist maps client token → view column
    private static readonly Dictionary<string, string> SortMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lastAppraisedDate"]  = "LastAppraisedDate DESC",
        ["ownerName"]          = "OwnerName ASC",
        ["engagementCount"]    = "EngagementCount DESC",
    };

    private const string SelectColumns = """
        SELECT
            Id,
            CollateralType,
            OwnerName,
            CreatedOn,
            ISNULL(EngagementCount, 0)      AS EngagementCount,
            LastAppraisedDate,
            LastAppraisedValue,
            Land_Province,
            IsUnderConstructionAtLastAppraisal,
            OverallConstructionProgressPercent,
            Condo_Province
        """;

    public async Task<GetCollateralCatalogResult> Handle(
        GetCollateralCatalogQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Collateral catalog is restricted to Admin users.");

        var sql = $"{SelectColumns} FROM collateral.vw_CollateralMasters WHERE 1 = 1";
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            sql += " AND CollateralType = @Type";
            p.Add("Type", query.Type.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            // Province applies to Land and Condo separately (different columns in the view)
            sql += " AND (Land_Province = @Province OR Condo_Province = @Province)";
            p.Add("Province", query.Province.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            sql += " AND OwnerName LIKE @OwnerPattern";
            p.Add("OwnerPattern", "%" + query.Owner.Trim() + "%");
        }

        if (query.IsUnderConstruction.HasValue)
        {
            sql += " AND CollateralType = 'Land' AND IsUnderConstructionAtLastAppraisal = @IsUnderConstruction";
            p.Add("IsUnderConstruction", query.IsUnderConstruction.Value ? 1 : 0);
        }

        if (query.MinAppraisals.HasValue)
        {
            sql += " AND ISNULL(EngagementCount, 0) >= @MinAppraisals";
            p.Add("MinAppraisals", query.MinAppraisals.Value);
        }

        if (query.LastAppraisedFrom.HasValue)
        {
            sql += " AND LastAppraisedDate >= @LastAppraisedFrom";
            p.Add("LastAppraisedFrom", query.LastAppraisedFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.LastAppraisedTo.HasValue)
        {
            sql += " AND LastAppraisedDate <= @LastAppraisedTo";
            p.Add("LastAppraisedTo", query.LastAppraisedTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        var orderBy = SortMap.TryGetValue(query.Sort ?? "", out var col)
            ? col
            : "CreatedOn DESC";

        var result = await connectionFactory.QueryPaginatedAsync<CollateralCatalogItemDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            p);

        return new GetCollateralCatalogResult(result);
    }
}
