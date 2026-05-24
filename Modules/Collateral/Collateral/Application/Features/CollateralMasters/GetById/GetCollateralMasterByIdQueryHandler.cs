using System.Text.Json;
using Appraisal.Contracts.Photos;
using Collateral.Application.Features.CollateralMasters.Lookup;
using Collateral.Application.Features.CollateralMasters.Shared;
using Dapper;

namespace Collateral.Application.Features.CollateralMasters.GetById;

/// <summary>
/// Returns the full master detail from vw_CollateralMasters.
/// For Leasehold masters, also fetches a lightweight underlying master summary.
/// Phase 2: also resolves photos lazily through the latest CollateralEngagement's appraisal.
/// </summary>
public class GetCollateralMasterByIdQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ISender mediator
) : IQueryHandler<GetCollateralMasterByIdQuery, GetCollateralMasterByIdResult>
{
    public async Task<GetCollateralMasterByIdResult> Handle(
        GetCollateralMasterByIdQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM collateral.vw_CollateralMasters WHERE Id = @Id";
        var row = await connectionFactory.QueryFirstOrDefaultAsync<CollateralMasterViewRow>(
            sql, new { query.Id });

        if (row is null)
            throw new NotFoundException("CollateralMaster", query.Id);

        UnderlyingMasterSummaryDto? underlyingMaster = null;

        // Lh_UnderlyingMasterId.HasValue is the actual signal — set on any master with a
        // LeaseholdDetails row (LSL, LSB, LS). Avoid re-enumerating type codes so a future
        // variant can't silently skip the underlying-master fetch.
        if (row.Lh_UnderlyingMasterId.HasValue)
        {
            underlyingMaster = await FetchUnderlyingMasterSummaryAsync(row.Lh_UnderlyingMasterId.Value);
        }

        // Phase 2: resolve photos through latest engagement → appraisal
        var photos = await ResolvePhotosAsync(query.Id, cancellationToken);

        return MapToResult(row, underlyingMaster, photos);
    }

    /// <summary>
    /// Finds the latest non-deleted CollateralEngagement for this master,
    /// extracts the AppraisalProperty IDs that belong to this master from the snapshot,
    /// then calls the cross-module query to retrieve mapped photos.
    ///
    /// Returns an empty list when:
    /// - No engagement exists for this master.
    /// - The snapshot cannot be parsed (malformed JSON — treated as no-op, not an error).
    /// - The appraisal has no IsInUse photos mapped to the relevant properties.
    /// </summary>
    private async Task<IReadOnlyList<CollateralPhotoDto>> ResolvePhotosAsync(
        Guid masterId,
        CancellationToken cancellationToken)
    {
        // Fetch the latest engagement for this master. Ordering matches sibling handlers
        // (GetMostRecentEngagementByPriorAppraisalQueryHandler etc.) — AppraisalDate is the
        // business-meaning date; CreatedAt breaks same-day ties deterministically without
        // relying on sequential-Id timing assumptions.
        var engagementSql = """
            SELECT TOP 1
                AppraisalId,
                Snapshot
            FROM collateral.CollateralEngagements
            WHERE CollateralMasterId = @MasterId
            ORDER BY AppraisalDate DESC, CreatedAt DESC
            """;

        var connection = connectionFactory.GetOpenConnection();
        var engRow = await connection.QueryFirstOrDefaultAsync<EngagementRow>(
            engagementSql, new { MasterId = masterId });

        if (engRow is null)
            return [];

        // Extract the AppraisalProperty IDs that belong to THIS master from the snapshot.
        // The snapshot shape is: { "groups": [ { "isMasterId": "...", "properties": [ { "collateralMasterId": "...", "propertyId": "..." } ] } ] }
        // We collect all propertyId values where collateralMasterId == masterId.
        var propertyIds = ExtractPropertyIdsForMaster(masterId, engRow.Snapshot);

        // Photo-scope safety: if the snapshot yielded no matching properties (master not in
        // snapshot, malformed JSON, or snapshot missing) treat as zero photos. The
        // cross-module query interprets an empty PropertyIds list as "no filter — all
        // photos on this appraisal", which on a multi-master appraisal would leak photos
        // belonging to OTHER masters. Short-circuit here instead.
        if (propertyIds.Count == 0)
            return [];

        var photosQuery = new GetAppraisalPhotosForCollateralQuery(
            engRow.AppraisalId,
            propertyIds);

        return await mediator.Send(photosQuery, cancellationToken);
    }

    /// <summary>
    /// Parses the engagement snapshot JSON and returns the AppraisalProperty IDs
    /// for entries whose collateralMasterId matches the given master ID.
    ///
    /// Returns an empty list (not null) on any parse failure so the caller can safely
    /// fall back to returning no photos rather than throwing.
    /// </summary>
    private static IReadOnlyList<Guid> ExtractPropertyIdsForMaster(
        Guid masterId,
        string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
            return [];

        try
        {
            var masterIdStr = masterId.ToString();
            var result = new List<Guid>();

            using var doc = JsonDocument.Parse(snapshot);
            if (!doc.RootElement.TryGetProperty("groups", out var groups))
                return [];

            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("properties", out var properties))
                    continue;

                foreach (var entry in properties.EnumerateArray())
                {
                    // collateralMasterId ties this snapshot entry to a specific master row
                    if (!entry.TryGetProperty("collateralMasterId", out var cmIdEl))
                        continue;

                    var cmId = cmIdEl.GetString();
                    if (!string.Equals(cmId, masterIdStr, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!entry.TryGetProperty("propertyId", out var propIdEl))
                        continue;

                    var propIdStr = propIdEl.GetString();
                    if (propIdStr is not null && Guid.TryParse(propIdStr, out var propId))
                        result.Add(propId);
                }
            }

            return result.Distinct().ToList();
        }
        catch (JsonException)
        {
            // Malformed snapshot — treat as no properties rather than surfacing a 500.
            return [];
        }
    }

    private async Task<UnderlyingMasterSummaryDto?> FetchUnderlyingMasterSummaryAsync(Guid underlyingId)
    {
        var sql = """
            SELECT
                Id,
                CollateralType,
                OwnerName,
                Land_Province     AS Province,
                Land_TitleNumber  AS TitleNumber,
                LastAppraisedDate,
                LastAppraisedValue
            FROM collateral.vw_CollateralMasters
            WHERE Id = @Id
            """;

        var row = await connectionFactory.QueryFirstOrDefaultAsync<UnderlyingRow>(
            sql, new { Id = underlyingId });

        if (row is null) return null;

        return new UnderlyingMasterSummaryDto(
            row.Id,
            row.CollateralType,
            row.OwnerName,
            row.Province,
            row.TitleNumber,
            row.LastAppraisedDate,
            row.LastAppraisedValue);
    }

    private static GetCollateralMasterByIdResult MapToResult(
        CollateralMasterViewRow row,
        UnderlyingMasterSummaryDto? underlying,
        IReadOnlyList<CollateralPhotoDto> photos)
    {
        LandDetailDto? landDetail = null;
        CondoDetailDto? condoDetail = null;
        LeaseholdDetailDto? leaseholdDetail = null;
        MachineDetailDto? machineDetail = null;

        // Family-grouped cases: a master's CollateralType may have flipped via LATEST-wins
        // (e.g. L → LB when a building was appraised). All variants share one detail DTO shape.
        switch (row.CollateralType)
        {
            case CollateralTypes.Land:
            case CollateralTypes.LandWithBuilding:
                landDetail = new LandDetailDto(
                    row.Land_LandOfficeCode!,
                    row.Land_Province!,
                    row.Land_District!,
                    row.Land_SubDistrict!,
                    row.Land_TitleType!,
                    row.Land_TitleNumber!,
                    row.Land_SurveyNumber,
                    row.Land_LandParcelNumber,
                    row.Land_Street,
                    row.Land_Village,
                    row.Land_Latitude,
                    row.Land_Longitude,
                    row.Land_LandShapeType,
                    row.Land_LandZoneType,
                    row.Land_UrbanPlanningType,
                    row.Land_AccessRoadWidth,
                    row.Land_RoadFrontage,
                    row.Land_LandArea,
                    row.IsUnderConstructionAtLastAppraisal ?? false,
                    row.OverallConstructionProgressPercent,
                    // PR-5: Land_LastConstructionInspectionId removed — CI list is in the engagement snapshot.
                    row.Land_LastAppraisalId,
                    row.Land_LastAppraisalNumber,
                    row.Land_LastAppraisedDate,
                    row.Land_UnitPrice,
                    row.Land_BuildingCost,
                    row.Land_AppraisalValue,
                    AliasTitles: []);   // GetById does not load alias titles — FE uses Lookup for that
                break;

            case CollateralTypes.Condo:
                condoDetail = new CondoDetailDto(
                    row.Condo_LandOfficeCode!,
                    row.Condo_CondoRegistrationNumber!,
                    row.Condo_BuildingNumber!,
                    row.Condo_FloorNumber!,
                    row.Condo_RoomNumber!,
                    row.Condo_TitleNumber!,
                    row.Condo_TitleType!,
                    row.Condo_CondoName,
                    row.Condo_Province,
                    row.Condo_UsableArea,
                    row.Condo_LocationType,
                    row.Condo_BuildingAge,
                    row.Condo_ConstructionYear,
                    row.Condo_ModelName,
                    row.Condo_LastAppraisalId,
                    row.Condo_LastAppraisalNumber,
                    row.Condo_LastAppraisedDate,
                    row.Condo_UnitPrice,
                    row.Condo_BuildingCost,
                    row.Condo_AppraisalValue);
                break;

            case CollateralTypes.Leasehold:
            case CollateralTypes.LeaseholdBuilding:
            case CollateralTypes.LeaseholdWithBuilding:
                leaseholdDetail = new LeaseholdDetailDto(
                    row.Lh_LeaseRegistrationNo!,
                    row.Lh_UnderlyingMasterId!.Value,
                    row.Lh_Lessor!,
                    row.Lh_Lessee!,
                    DateOnly.FromDateTime(row.Lh_LeaseTermStart!.Value),
                    row.Lh_LeaseTermEnd.HasValue ? DateOnly.FromDateTime(row.Lh_LeaseTermEnd.Value) : null,
                    row.Lh_LeaseTermMonths,
                    row.Lh_LastAppraisalId,
                    row.Lh_LastAppraisalNumber,
                    row.Lh_LastAppraisedDate);
                break;

            case CollateralTypes.Machine:
                machineDetail = new MachineDetailDto(
                    row.Machine_MachineRegistrationNo,
                    row.Machine_SerialNo,
                    row.Machine_Brand,
                    row.Machine_Model,
                    row.Machine_Manufacturer,
                    row.Machine_LastAppraisalId,
                    row.Machine_LastAppraisalNumber,
                    row.Machine_LastAppraisedDate);
                break;
        }

        return new GetCollateralMasterByIdResult(
            row.Id,
            row.CollateralType,
            row.OwnerName,
            row.CreatedAt,
            row.UpdatedAt,
            row.EngagementCount ?? 0,
            row.LastAppraisedDate,
            row.LastAppraisedValue,
            landDetail,
            condoDetail,
            leaseholdDetail,
            machineDetail,
            underlying,
            photos);
    }

    // Small Dapper projection for the latest engagement row
    private class EngagementRow
    {
        public Guid AppraisalId { get; init; }
        public string Snapshot { get; init; } = null!;
    }

    // Small Dapper projection for the underlying master summary query
    private class UnderlyingRow
    {
        public Guid Id { get; init; }
        public string CollateralType { get; init; } = null!;
        public string? OwnerName { get; init; }
        public string? Province { get; init; }
        public string? TitleNumber { get; init; }
        public DateTime? LastAppraisedDate { get; init; }
        public decimal? LastAppraisedValue { get; init; }
    }
}
