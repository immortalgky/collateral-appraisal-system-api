namespace Collateral.Application.Features.CollateralEngagements.SearchEngagements;

/// <summary>
/// GET /collateral-engagements/search
/// Engagement-grain search — one row per past appraisal event, not per collateral master.
///
/// Filters:
///   appraisalReportNo      — LIKE on AppraisalNumber
///   appraisalDateFrom/To   — range on AppraisalDate
///   titleDeedNo            — LIKE on Land/Condo title number or Leasehold registration
///   collateralType         — multi-select on AppraisedCollateralType (e.g. L, LB, U, LSL, MAC)
///   buildingTypeCode       — multi-select; EXISTS on CollateralEngagementBuildings
///   landAreaFrom/To        — range on LandAreaInSqWa (sq.wa)
///   customerName           — LIKE on OwnerName
///   centerLat/Lng/radiusKm — geo circle (Land or Condo)
///   subDistrict/district/province — address match
/// </summary>
public class SearchCollateralEngagementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-engagements/search",
                async (
                    [AsParameters] PaginationRequest paginationRequest,
                    string? appraisalReportNo,
                    DateOnly? appraisalDateFrom,
                    DateOnly? appraisalDateTo,
                    string? titleDeedNo,
                    string[]? collateralType,
                    string[]? buildingTypeCode,
                    decimal? landAreaFrom,
                    decimal? landAreaTo,
                    string? customerName,
                    decimal? centerLat,
                    decimal? centerLng,
                    decimal? radiusKm,
                    string? subDistrict,
                    string? district,
                    string? province,
                    Guid? collateralMasterId,
                    string? sort,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Geo circle requires all three params together. Reject partial input so the
                    // FE doesn't silently lose its filter. Also validate value ranges so a
                    // negative radius or out-of-range lat/lng doesn't quietly produce zero hits.
                    var geoParts = new[] { centerLat.HasValue, centerLng.HasValue, radiusKm.HasValue };
                    var geoSet = geoParts.Count(x => x);
                    if (geoSet > 0 && geoSet < 3)
                    {
                        return Results.BadRequest(new
                        {
                            error = "centerLat, centerLng, and radiusKm must all be supplied together (or all omitted)."
                        });
                    }
                    if (geoSet == 3)
                    {
                        if (radiusKm!.Value <= 0)
                            return Results.BadRequest(new { error = "radiusKm must be greater than 0." });
                        if (centerLat!.Value < -90m || centerLat.Value > 90m)
                            return Results.BadRequest(new { error = "centerLat must be in [-90, 90]." });
                        if (centerLng!.Value < -180m || centerLng.Value > 180m)
                            return Results.BadRequest(new { error = "centerLng must be in [-180, 180]." });
                    }

                    var query = new SearchCollateralEngagementsQuery(
                        PaginationRequest: paginationRequest,
                        AppraisalReportNo: appraisalReportNo,
                        AppraisalDateFrom: appraisalDateFrom,
                        AppraisalDateTo: appraisalDateTo,
                        TitleDeedNo: titleDeedNo,
                        CollateralTypes: collateralType,
                        BuildingTypeCodes: buildingTypeCode,
                        LandAreaFromSqWa: landAreaFrom,
                        LandAreaToSqWa: landAreaTo,
                        CustomerName: customerName,
                        CenterLat: centerLat,
                        CenterLng: centerLng,
                        RadiusKm: radiusKm,
                        SubDistrict: subDistrict,
                        District: district,
                        Province: province,
                        CollateralMasterId: collateralMasterId,
                        Sort: sort);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result.Items);
                }
            )
            .WithName("SearchCollateralEngagements")
            .Produces<PaginatedResult<CollateralEngagementSearchItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Search collateral engagements (one row per appraisal event)")
            .WithDescription(
                "Engagement-grain search. Each result is a distinct past appraisal of a collateral. " +
                "Filters use engagement-time values (AppraisedCollateralType, LandAreaInSqWa) " +
                "so results are historically accurate even if the master has since been re-classified.")
            .WithTags("CollateralEngagement")
            // Internal-only data (green-pin engagements); same policy as the Level-1 history-search
            // endpoint. The handler also short-circuits external users as the load-bearing guard.
            .RequireAuthorization("history-search.view");
    }
}
