using System.Text.Json;
using Appraisal.Contracts.Appraisals;
using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.GetCollateralEngagementDetail;

/// <summary>
/// Returns the structured detail for a single collateral engagement, including:
///  - Round meta from the CollateralEngagements row.
///  - Identity of the clicked collateral master (resolved via Snapshot + cross-module appraisal query).
///  - All appraisal properties grouped by GroupNumber.
///
/// Data sources:
///  1. Dapper — collateral.CollateralEngagements JOIN collateral.CollateralMasters
///  2. In-process MediatR — GetAppraisalForCollateralQuery (same pattern as CollateralMasterUpsertService)
/// </summary>
public class GetCollateralEngagementDetailQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ISender mediator,
    ICurrentUserService currentUser
) : IQueryHandler<GetCollateralEngagementDetailQuery, GetCollateralEngagementDetailResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GetCollateralEngagementDetailResult> Handle(
        GetCollateralEngagementDetailQuery query,
        CancellationToken cancellationToken)
    {
        // Visibility: collateral engagement detail is green-pin data, internal-only
        // (mirrors HistorySearchQueryHandler / SearchCollateralEngagementsQueryHandler).
        // 404 rather than 403 so we don't leak the engagement's existence to externals.
        if (currentUser.IsExternal)
            throw new NotFoundException("CollateralEngagement", query.EngagementId);

        // -----------------------------------------------------------------------
        // 1. Load the engagement row (same SQL as GetEngagementSnapshotQueryHandler)
        // -----------------------------------------------------------------------
        var sql = """
            SELECT
                e.AppraisalId,
                e.AppraisalNumber,
                e.AppraisalDate,
                e.AppraisalType,
                e.AppraisalValue,
                e.Snapshot
            FROM collateral.CollateralEngagements e
            INNER JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
            WHERE e.Id                = @EngagementId
              AND e.CollateralMasterId = @MasterId
              AND m.IsDeleted          = 0
            """;

        var p = new DynamicParameters();
        p.Add("EngagementId", query.EngagementId);
        p.Add("MasterId",     query.CollateralMasterId);

        var row = await connectionFactory.QueryFirstOrDefaultAsync<EngagementRow>(sql, p);

        if (row is null)
            throw new NotFoundException("CollateralEngagement", query.EngagementId);

        // -----------------------------------------------------------------------
        // 2. Load rich appraisal data via cross-module query
        // -----------------------------------------------------------------------
        var appraisal = await mediator.Send(
            new GetAppraisalForCollateralQuery(row.AppraisalId), cancellationToken);

        if (appraisal is null)
            throw new NotFoundException("Appraisal", row.AppraisalId);

        // -----------------------------------------------------------------------
        // 3. Parse snapshot to find propertyId(s) belonging to this master
        // -----------------------------------------------------------------------
        var matchedPropertyIds = ExtractPropertyIdsForMaster(
            query.CollateralMasterId, row.Snapshot);

        // -----------------------------------------------------------------------
        // 4. Resolve the primary matched property for identity
        // -----------------------------------------------------------------------
        var allProperties = appraisal.Properties;

        // Find the matched property. When multiple properties are matched (e.g. aliased),
        // prefer the one marked as isMaster in the snapshot — here we pick the first
        // matched property; in practice a collateralMasterId maps to exactly one propertyId.
        AppraisalPropertyForCollateral? matchedProperty = null;
        if (matchedPropertyIds.Count > 0)
        {
            matchedProperty = allProperties
                .FirstOrDefault(p => matchedPropertyIds.Contains(p.PropertyId));
        }

        // -----------------------------------------------------------------------
        // 5. Build result DTOs
        // -----------------------------------------------------------------------
        var meta = BuildMeta(query.EngagementId, row);
        var identity = BuildIdentity(matchedProperty, allProperties);
        var groups = BuildGroups(allProperties);

        return new GetCollateralEngagementDetailResult(meta, identity, groups);
    }

    // ---------------------------------------------------------------------------
    // Meta
    // ---------------------------------------------------------------------------
    private static RoundMetaDto BuildMeta(Guid engagementId, EngagementRow row) =>
        new(
            engagementId,
            row.AppraisalId,
            row.AppraisalNumber,
            row.AppraisalDate,
            row.AppraisalType,
            row.AppraisalValue
        );

    // ---------------------------------------------------------------------------
    // Identity for the clicked collateral
    // ---------------------------------------------------------------------------
    private static CollateralIdentityDto BuildIdentity(
        AppraisalPropertyForCollateral? matched,
        IReadOnlyList<AppraisalPropertyForCollateral> allProperties)
    {
        if (matched is null)
            return new CollateralIdentityDto(
                null, null, null, null, null, null, null, null, null, null, null, null);

        // Find a Building property in the same group (if any) for BuildingTypeCode
        var groupNumber = matched.GroupNumber;
        var buildingInGroup = groupNumber.HasValue
            ? allProperties.FirstOrDefault(p =>
                p.GroupNumber == groupNumber &&
                p.PropertyTypeCode is "B" or "LB" &&
                p.BuildingIdentity is not null)
            : null;

        string? buildingTypeCode = buildingInGroup?.BuildingIdentity?.BuildingTypeCode;
        string? projectOrVillageName = null;
        string? street = null;
        string? subDistrict = null;
        string? district = null;
        string? province = null;
        decimal? latitude = null;
        decimal? longitude = null;
        decimal? landAreaInSqWa = null;
        decimal? buildingOrUsableArea = null;
        string? modelName = null;

        switch (matched.PropertyTypeCode)
        {
            case "U": // Condo
            {
                var condo = matched.CondoIdentity;
                projectOrVillageName = condo?.CondoName;
                province = condo?.Province;
                latitude = condo?.Latitude;
                longitude = condo?.Longitude;
                buildingOrUsableArea = condo?.UsableArea;
                modelName = condo?.ModelName;
                break;
            }

            case "L":
            case "LB":
            {
                var land = matched.LandIdentity;
                projectOrVillageName = land?.Village;
                street = land?.Street;
                subDistrict = land?.SubDistrict;
                district = land?.District;
                province = land?.Province;
                latitude = land?.Latitude;
                longitude = land?.Longitude;
                landAreaInSqWa = land?.LandArea;
                buildingOrUsableArea = buildingInGroup?.BuildingIdentity?.BuildingArea;
                break;
            }

            case "B": // standalone building
            {
                var building = matched.BuildingIdentity;
                buildingOrUsableArea = building?.BuildingArea;
                buildingTypeCode = building?.BuildingTypeCode;
                break;
            }

            case "LSL":
            case "LSB":
            case "LS":
            {
                // Leasehold — no strong geo info; latitude/longitude stay null
                break;
            }

            case "M": // Machinery
            {
                // No area or geo fields exposed on this screen
                break;
            }
        }

        return new CollateralIdentityDto(
            matched.PropertyTypeCode,
            buildingTypeCode,
            projectOrVillageName,
            street,
            subDistrict,
            district,
            province,
            latitude,
            longitude,
            landAreaInSqWa,
            buildingOrUsableArea,
            modelName
        );
    }

    // ---------------------------------------------------------------------------
    // Property groups
    // ---------------------------------------------------------------------------
    private static IReadOnlyList<PropertyGroupDto> BuildGroups(
        IReadOnlyList<AppraisalPropertyForCollateral> allProperties)
    {
        // Only include properties that are assigned to a group
        var grouped = allProperties
            .Where(p => p.GroupNumber.HasValue)
            .GroupBy(p => p.GroupNumber!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new PropertyGroupDto(
                g.Key,
                g.OrderBy(p => p.SequenceInGroup ?? int.MaxValue)
                 .Select(p => new PropertySummaryDto(
                     p.PropertyId,
                     ResolvePropertyName(p),
                     p.PropertyTypeCode,
                     ResolveArea(p),
                     ResolveLatitude(p),
                     ResolveLongitude(p)
                 ))
                 .ToList()
            ))
            .ToList();

        return grouped;
    }

    private static string? ResolvePropertyName(AppraisalPropertyForCollateral p) =>
        p.PropertyTypeCode switch
        {
            "U" => p.CondoIdentity?.RoomNumber
                   ?? p.CondoIdentity?.CondoName,
            "L" or "LB" => p.LandIdentity?.Titles.FirstOrDefault()?.TitleNumber,
            "M" => p.MachineryIdentity?.RegistrationNumber ?? p.MachineryIdentity?.SerialNo,
            "LSL" or "LSB" or "LS" => p.LeaseholdIdentity?.ContractNo,
            "B" => p.BuildingIdentity?.BuiltOnTitleNumber,
            _ => null
        };

    private static decimal? ResolveArea(AppraisalPropertyForCollateral p) =>
        p.PropertyTypeCode switch
        {
            "U" => p.CondoIdentity?.UsableArea,
            "L" or "LB" => p.LandIdentity?.LandArea,
            "B" => p.BuildingIdentity?.BuildingArea,
            _ => null
        };

    private static decimal? ResolveLatitude(AppraisalPropertyForCollateral p) =>
        p.PropertyTypeCode switch
        {
            "U" => p.CondoIdentity?.Latitude,
            "L" or "LB" => p.LandIdentity?.Latitude,
            _ => null
        };

    private static decimal? ResolveLongitude(AppraisalPropertyForCollateral p) =>
        p.PropertyTypeCode switch
        {
            "U" => p.CondoIdentity?.Longitude,
            "L" or "LB" => p.LandIdentity?.Longitude,
            _ => null
        };

    // ---------------------------------------------------------------------------
    // Snapshot parsing — find propertyIds for the clicked collateralMasterId
    // (Same logic as GetCollateralMasterByIdQueryHandler.ExtractPropertyIdsForMaster)
    // ---------------------------------------------------------------------------
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
                    if (!entry.TryGetProperty("collateralMasterId", out var cmIdEl))
                        continue;

                    if (!string.Equals(
                            cmIdEl.GetString(), masterIdStr,
                            StringComparison.OrdinalIgnoreCase))
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
            return [];
        }
    }

    // ---------------------------------------------------------------------------
    // Dapper projection for the engagement row
    // ---------------------------------------------------------------------------
    private class EngagementRow
    {
        public Guid AppraisalId { get; init; }
        public string? AppraisalNumber { get; init; }
        public DateTime? AppraisalDate { get; init; }
        public string? AppraisalType { get; init; }
        public decimal? AppraisalValue { get; init; }
        public string? Snapshot { get; init; }
    }
}
