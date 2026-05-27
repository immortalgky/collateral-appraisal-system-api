using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalMapPins;

/// <summary>
/// Returns the appraisal's own collateral pin locations (land + condo properties that carry
/// coordinates) and its own linked market-comparable pins.
///
/// The in-progress appraisal is excluded from POST /history-search (which requires
/// CompletedAt IS NOT NULL). This dedicated endpoint fills that gap for the 360-summary map.
///
/// Visibility enforcement (enforced server-side in this handler — never trust the client):
///   Internal (bank) callers — <see cref="AppraisalAccessScope.GetEnforcedCompanyId"/> returns
///     null; both queries run without company filters.
///   External (company) callers — <c>enforcedCompanyId</c> is set:
///     • Collateral pins: only returned if the appraisal's latest non-rejected assignment
///       belongs to that company (via vw_AppraisalList.AssigneeCompanyId). If not, empty list.
///     • MC pins: filtered to <c>mc.CreatedByCompanyId = @CompanyId</c>, matching the
///       HistorySearchQueryHandler external branch.
///
/// Two sequential Dapper queries share the scope-supplied connection — sequential execution
/// avoids the no-MARS limitation without the cost of two pooled connections.
/// </summary>
public class GetAppraisalMapPinsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetAppraisalMapPinsQuery, GetAppraisalMapPinsResult>
{
    public async Task<GetAppraisalMapPinsResult> Handle(
        GetAppraisalMapPinsQuery query,
        CancellationToken cancellationToken)
    {
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);

        // ── Collateral-access gate for external callers ───────────────────────
        // External users can only see collateral pins for appraisals assigned to
        // their own company. Check up front using vw_AppraisalList.AssigneeCompanyId
        // (the latest active assignment's company, same as the list endpoint uses).
        // If the appraisal is not theirs, return empty rather than 403/500.
        if (enforcedCompanyId.HasValue)
        {
            var accessParams = new DynamicParameters();
            accessParams.Add("AppraisalId", query.AppraisalId);
            accessParams.Add("CompanyId", enforcedCompanyId.Value);

            const string accessSql = """
                SELECT COUNT(1)
                FROM appraisal.vw_AppraisalList al
                WHERE al.Id = @AppraisalId
                  AND TRY_CAST(al.AssigneeCompanyId AS uniqueidentifier) = @CompanyId
                """;

            var count = await connectionFactory.ExecuteScalarAsync<int>(accessSql, accessParams);
            if (count == 0)
            {
                // Appraisal either doesn't exist or is not assigned to this company.
                // Return empty — do not reveal whether the appraisal exists at all.
                return new GetAppraisalMapPinsResult([], []);
            }
        }

        // ── Query 1: collateral pins ──────────────────────────────────────────
        // UNION LandAppraisalDetails and CondoAppraisalDetails for the given
        // AppraisalId, returning one row per property that has non-null coords.
        // No additional company filter needed here: external access was already
        // gated by the assignment check above.
        var collateralSql = """
            SELECT
                ap.Id           AS AppraisalPropertyId,
                ld.Latitude     AS Lat,
                ld.Longitude    AS Lon,
                ap.PropertyType AS PropertyType,
                ld.Province     AS Province,
                ld.District     AS District,
                ld.SubDistrict  AS SubDistrict
            FROM appraisal.AppraisalProperties ap
            JOIN appraisal.LandAppraisalDetails ld ON ld.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
              AND ld.Latitude  IS NOT NULL
              AND ld.Longitude IS NOT NULL

            UNION ALL

            SELECT
                ap.Id           AS AppraisalPropertyId,
                cd.Latitude     AS Lat,
                cd.Longitude    AS Lon,
                ap.PropertyType AS PropertyType,
                cd.Province     AS Province,
                cd.District     AS District,
                cd.SubDistrict  AS SubDistrict
            FROM appraisal.AppraisalProperties ap
            JOIN appraisal.CondoAppraisalDetails cd ON cd.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
              AND cd.Latitude  IS NOT NULL
              AND cd.Longitude IS NOT NULL

            ORDER BY AppraisalPropertyId
            """;

        var collateralParams = new DynamicParameters();
        collateralParams.Add("AppraisalId", query.AppraisalId);

        var collateral = (await connectionFactory.QueryAsync<AppraisalMapCollateralPinDto>(
            collateralSql, collateralParams)).ToList();

        // ── Query 2: market-comparable pins ──────────────────────────────────
        // Join AppraisalComparables to MarketComparables. Filter soft-deleted MCs
        // and those without coordinates.
        // External callers are additionally scoped to their own company's MCs
        // (mc.CreatedByCompanyId = @CompanyId) — matches HistorySearchQueryHandler.
        var mcSql = """
            SELECT
                mc.Id            AS MarketComparableId,
                mc.Latitude      AS Lat,
                mc.Longitude     AS Lon,
                mc.PropertyType  AS PropertyType,
                mc.SurveyName    AS SurveyName,
                mc.InfoDateTime  AS InfoDateTime,
                mc.OfferPrice    AS OfferPrice,
                mc.SalePrice     AS SalePrice
            FROM appraisal.AppraisalComparables ac
            JOIN appraisal.MarketComparables mc ON mc.Id = ac.MarketComparableId
            WHERE ac.AppraisalId  = @AppraisalId
              AND mc.IsDeleted    = 0
              AND mc.Latitude    IS NOT NULL
              AND mc.Longitude   IS NOT NULL
            """;

        var mcParams = new DynamicParameters();
        mcParams.Add("AppraisalId", query.AppraisalId);

        if (enforcedCompanyId.HasValue)
        {
            mcSql += " AND mc.CreatedByCompanyId = @CompanyId";
            mcParams.Add("CompanyId", enforcedCompanyId.Value);
        }

        mcSql += " ORDER BY mc.Id";

        var marketComparables = (await connectionFactory.QueryAsync<AppraisalMapComparablePinDto>(
            mcSql, mcParams)).ToList();

        return new GetAppraisalMapPinsResult(collateral, marketComparables);
    }
}
