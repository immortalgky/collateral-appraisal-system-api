using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the Building Details section (FSD §2.1.2.5 รายละเอียดอาคาร) for a given appraisal.
///
/// Strategy (read-only Dapper, no EF tracking):
///   Step 1 — resolve the first building AppraisalProperty for the appraisal.
///   Step 2 — load BuildingAppraisalDetails header row + parameter translations.
///   Step 3 — load BuildingDepreciationDetails cost rows, compute totals in C#.
///
/// Multiple buildings per appraisal are not uncommon, but the FSD section shows ONE building.
/// This loader targets the first building property (lowest SequenceNumber). Multi-building
/// support can be added later by returning a list and letting the template loop.
///
/// Returns <see langword="null"/> when the appraisal has no building property.
/// </summary>
internal static class BuildingSectionLoader
{
    /// <summary>
    /// Loads ONE <see cref="BuildingSection"/> per building-bearing property (B / LB / lease)
    /// for the appraisal, each tagged with its property group and carrying its own cost rows
    /// and floor surfaces. Returns an empty list when the appraisal has no building properties.
    /// </summary>
    public static async Task<List<BuildingSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var appraisalParams = new DynamicParameters();
        appraisalParams.Add("AppraisalId", appraisalId);

        // ── Batch: 3 result sets, single round-trip ───────────────────────────────
        //
        // Step 1 (RS01) — Resolve first building property; target the lowest
        //                 SequenceNumber for type code 'Building'.
        // Step 2 (RS02) — Building header + parameter translations. Rewritten to
        //                 derive PropertyId via subquery so no separate round-trip
        //                 is needed.
        // Step 3 (RS03) — Depreciation/cost rows. Rewritten to derive
        //                 BuildingAppraisalDetailId via a double-hop subquery through
        //                 @AppraisalId → first 'Building' property → bad.Id.
        //
        // All three result sets are parameterised only on @AppraisalId.
        //
        // Step 2 column notes (BuildingAppraisalDetailConfiguration.cs):
        //   BuildingType / BuildingConditionType are CODE strings translated via
        //   parameter.Parameters with COALESCE fallback (consistent with Q16 in
        //   AppraisalSummaryLandBuildingDataProvider).
        //
        // Step 3 column notes (BuildingAppraisalDetailConfiguration OwnsMany):
        //   AreaDescription, Area, PricePerSqMBeforeDepreciation, PriceBeforeDepreciation,
        //   Year, DepreciationYearPct, TotalDepreciationPct, PriceDepreciation,
        //   PriceAfterDepreciation. Ordered by Id (insertion order).
        const string batchSql = """
            -- RS01: Building header per building-bearing property, with its property group.
            -- PropertyType is stored as a code ('B','LB','LSB','LS' all carry building
            -- detail), so we key off the existence of a BuildingAppraisalDetails row
            -- rather than matching a type string. GroupNumber 0 = ungrouped fallback.
            SELECT
                bad.Id                        AS BuildingDetailId,
                COALESCE(pg.GroupNumber, 0)   AS GroupNumber,
                pg.GroupName,
                bad.HouseNumber,
                bad.BuildingType,
                bad.BuildingTypeOther,
                CASE WHEN bad.BuildingType = '99' AND NULLIF(bad.BuildingTypeOther, '') IS NOT NULL
                     THEN bad.BuildingTypeOther
                     ELSE COALESCE(ptBT.Description, bad.BuildingTypeOther, bad.BuildingType) END
                                              AS BuildingTypeText,
                bad.NumberOfFloors,
                bad.ModelName,
                bad.PropertyName              AS BuildingName,
                bad.OwnerName,
                bad.BuiltOnTitleNumber,
                bad.TotalBuildingArea         AS UsableArea,
                bad.BuildingAge,
                bad.BuildingConditionType,
                bad.BuildingConditionTypeOther,
                CASE WHEN bad.BuildingConditionType = '99' AND NULLIF(bad.BuildingConditionTypeOther, '') IS NOT NULL
                     THEN bad.BuildingConditionTypeOther
                     ELSE COALESCE(ptBC.Description, bad.BuildingConditionTypeOther, bad.BuildingConditionType) END
                                              AS BuildingCondition,
                bad.BuildingMaterialType      AS MaterialQuality,
                bad.BuildingStyleType         AS BuildingForm,
                bad.UtilizationType,
                bad.UtilizationTypeOther,
                bad.StructureType,
                bad.StructureTypeOther,
                bad.RoofFrameType,
                bad.RoofFrameTypeOther,
                bad.RoofType,
                bad.RoofTypeOther,
                bad.CeilingType,
                bad.CeilingTypeOther,
                bad.InteriorWallType,
                bad.InteriorWallTypeOther,
                bad.ExteriorWallType,
                bad.ExteriorWallTypeOther,
                bad.DecorationType,
                bad.DecorationTypeOther,
                bad.ConstructionType,
                bad.ConstructionTypeOther,
                bad.Remark
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.PropertyGroups     pg  ON pg.Id = pgi.PropertyGroupId
            LEFT JOIN parameter.Parameters ptBT
                ON ptBT.[Group]    = 'BuildingType'
               AND ptBT.[Language] = 'TH'
               AND ptBT.[Code]     = bad.BuildingType
               AND ptBT.IsActive   = 1
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group]    = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code]     = bad.BuildingConditionType
               AND ptBC.IsActive   = 1
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY COALESCE(pg.GroupNumber, 0), pgi.SequenceInGroup, ap.SequenceNumber, ap.Id;

            -- RS02: Depreciation/cost rows for ALL buildings, keyed by parent for grouping.
            SELECT
                bdd.BuildingAppraisalDetailId,
                bdd.AreaDescription,
                bdd.Area,
                bdd.PricePerSqMBeforeDepreciation,
                bdd.PriceBeforeDepreciation,
                bdd.Year                       AS AgeYears,
                bdd.DepreciationYearPct,
                bdd.TotalDepreciationPct,
                bdd.PriceDepreciation          AS DepreciationAmount,
                bdd.PriceAfterDepreciation
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY bdd.BuildingAppraisalDetailId, bdd.Id;

            -- RS03: Thai descriptions for every coded structure/material field.
            -- Consumed as a group→(code→description) lookup; ResolveCodes falls back to the
            -- raw code for any value not present in the map.
            SELECT [group] AS [Group], [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN (
                  'BuildingMaterial', 'BuildingStyle', 'Utilization', 'GeneralStructure',
                  'RoofFrame', 'Roof', 'Ceiling', 'Interior', 'Exterior',
                  'Decoration', 'ConstructionType', 'FloorType', 'FloorStructure', 'FloorSurface');

            -- RS04: Per-floor-range surface rows (FSD #22–26) for ALL buildings, keyed by parent.
            SELECT
                bas.BuildingAppraisalDetailId,
                bas.FromFloorNumber,
                bas.ToFloorNumber,
                bas.FloorType,
                bas.FloorStructureType,
                bas.FloorStructureTypeOther,
                bas.FloorSurfaceType,
                bas.FloorSurfaceTypeOther
            FROM appraisal.BuildingAppraisalSurfaces bas
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bas.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY bas.BuildingAppraisalDetailId, bas.FromFloorNumber, bas.Id;
            """;

        List<BuildingHeaderRow> headerRows;
        List<DepreciationRow> rawRows;
        List<ParamRow> paramRows;
        List<SurfaceRow> surfaceRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, appraisalParams))
        {
            // RS01 — building headers (one per building property)
            headerRows = (await multi.ReadAsync<BuildingHeaderRow>()).ToList();

            // RS02 — depreciation rows (always read to drain the result set)
            rawRows = (await multi.ReadAsync<DepreciationRow>()).ToList();

            // RS03 — parameter code→Thai maps
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS04 — floor surface rows
            surfaceRows = (await multi.ReadAsync<SurfaceRow>()).ToList();
        }

        if (headerRows.Count == 0)
            return [];

        // ── Parameter code→Thai lookup, grouped by parameter group ────────────────
        var paramMaps = paramRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Group) && !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Group!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, string?>)g
                    .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Description, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        // Resolves a (group, raw code or JSON array) to comma-joined Thai descriptions.
        // Code "99" = "other": the free-text remark REPLACES the "อื่นๆ" label in place
        // (falling back to the resolved 99 description when the remark is blank). When the
        // array has no 99 code, a present remark is appended at the end (legacy safety).
        const string OtherCode = "99";
        string? ResolveCodes(string group, string? raw, string? other = null)
        {
            var map = paramMaps.GetValueOrDefault(group);
            var trimmedOther = string.IsNullOrWhiteSpace(other) ? null : other.Trim();
            var codes = ParseJsonArray(raw).ToList();
            var hasOtherCode = codes.Any(c => c == OtherCode);
            var items = codes
                .Select(c =>
                {
                    var display = map != null && map.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d! : c;
                    return c == OtherCode && trimmedOther != null ? trimmedOther : display;
                })
                .ToList();
            if (trimmedOther != null && !hasOtherCode)
                items.Add(trimmedOther);
            return items.Count > 0 ? string.Join(", ", items) : null;
        }

        // Cost rows and floor surfaces grouped by their parent building.
        var depByBuilding = rawRows
            .GroupBy(r => r.BuildingAppraisalDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var surfacesByBuilding = surfaceRows
            .GroupBy(s => s.BuildingAppraisalDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Build one BuildingSection per building property ───────────────────────
        var sections = new List<BuildingSection>(headerRows.Count);
        foreach (var hdr in headerRows)
        {
            var deps = depByBuilding.GetValueOrDefault(hdr.BuildingDetailId) ?? [];
            var surfaces = surfacesByBuilding.GetValueOrDefault(hdr.BuildingDetailId) ?? [];

            var costRows = deps
                .Select((r, i) => new BuildingCostRow
                {
                    Sequence = i + 1,
                    AreaItemName = r.AreaDescription,
                    Area = r.Area == 0 ? null : r.Area,
                    PricePerSqm = r.PricePerSqMBeforeDepreciation == 0 ? null : r.PricePerSqMBeforeDepreciation,
                    Price = r.PriceBeforeDepreciation == 0 ? null : r.PriceBeforeDepreciation,
                    AgeYears = r.AgeYears == 0 ? null : r.AgeYears,
                    DepreciationPctPerYear = r.DepreciationYearPct == 0 ? null : r.DepreciationYearPct,
                    TotalDepreciationPct = r.TotalDepreciationPct == 0 ? null : r.TotalDepreciationPct,
                    DepreciationAmount = r.DepreciationAmount == 0 ? null : r.DepreciationAmount,
                    ValueAfterDepreciation = r.PriceAfterDepreciation == 0 ? null : r.PriceAfterDepreciation,
                })
                .ToList();

            // Totals (computed in C# to avoid a second SQL round-trip)
            decimal? totalArea = deps.Count > 0 ? deps.Sum(r => r.Area) : null;
            decimal? totalPrice = deps.Count > 0 ? deps.Sum(r => r.PriceBeforeDepreciation) : null;
            decimal? totalValueAfterDepr = deps.Count > 0 ? deps.Sum(r => r.PriceAfterDepreciation) : null;

            // Resolve coded structure/material columns (JSON arrays of codes) to Thai.
            string? wall = BuildWallText(
                ResolveCodes("Exterior", hdr.ExteriorWallType, hdr.ExteriorWallTypeOther),
                ResolveCodes("Interior", hdr.InteriorWallType, hdr.InteriorWallTypeOther));

            // Floor surface rows (FSD #22–26)
            var floors = surfaces
                .Select(s => new BuildingFloorRow
                {
                    FromFloor = s.FromFloorNumber,
                    ToFloor = s.ToFloorNumber,
                    FloorType = ResolveCodes("FloorType", s.FloorType),
                    FloorStructure = ResolveCodes("FloorStructure", s.FloorStructureType, s.FloorStructureTypeOther),
                    FloorSurface = ResolveCodes("FloorSurface", s.FloorSurfaceType, s.FloorSurfaceTypeOther),
                })
                .ToList();

            sections.Add(new BuildingSection
            {
                GroupNumber = hdr.GroupNumber,
                GroupName = hdr.GroupName,
                HouseNumber = hdr.HouseNumber,
                BuildingTypeText = hdr.BuildingTypeText,
                NumberOfFloors = hdr.NumberOfFloors,
                ModelName = hdr.ModelName,
                BuildingName = hdr.BuildingName,
                OwnerName = hdr.OwnerName,
                BuiltOnTitleNumber = hdr.BuiltOnTitleNumber,
                LicenseSource = null,   // no source
                LicenseNumber = null,   // no source
                UsableArea = hdr.UsableArea,
                SizeText = null,        // no source (no width/length columns)
                BuildingAge = hdr.BuildingAge,
                BuildingCondition = hdr.BuildingCondition,
                Maintenance = null,     // no source
                MaterialQuality = ResolveCodes("BuildingMaterial", hdr.MaterialQuality),
                BuildingForm = ResolveCodes("BuildingStyle", hdr.BuildingForm),
                Utilization = ResolveCodes("Utilization", hdr.UtilizationType, hdr.UtilizationTypeOther),
                MainStructure = ResolveCodes("GeneralStructure", hdr.StructureType, hdr.StructureTypeOther),
                RoofFrame = ResolveCodes("RoofFrame", hdr.RoofFrameType, hdr.RoofFrameTypeOther),
                RoofMaterial = ResolveCodes("Roof", hdr.RoofType, hdr.RoofTypeOther),
                Wall = wall,
                Ceiling = ResolveCodes("Ceiling", hdr.CeilingType, hdr.CeilingTypeOther),
                Painting = ResolveCodes("Decoration", hdr.DecorationType, hdr.DecorationTypeOther),
                Door = null,            // no source
                Window = null,          // no source
                Sanitary = null,        // no source
                Decoration = ResolveCodes("ConstructionType", hdr.ConstructionType, hdr.ConstructionTypeOther),
                Floors = floors,
                CostRows = costRows,
                TotalArea = totalArea == 0 ? null : totalArea,
                TotalPrice = totalPrice == 0 ? null : totalPrice,
                TotalValueAfterDepreciation = totalValueAfterDepr == 0 ? null : totalValueAfterDepr,
                Remark = hdr.Remark,
            });
        }

        return sections;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a raw JSON array string (as stored by EF Core's nvarchar conversion) into a
    /// list of non-empty strings. Returns an empty list for null / whitespace / non-JSON input.
    /// </summary>
    private static List<string> ParseJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var s = raw.Trim();
        if (!s.StartsWith('['))
            return [s];

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s);
            return items?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [s];
        }
    }

    /// <summary>
    /// Combines exterior and interior wall display strings into a single label.
    /// If both are present and differ, formats as "ภายนอก: X / ภายใน: Y".
    /// </summary>
    private static string? BuildWallText(string? exterior, string? interior)
    {
        bool hasExt = !string.IsNullOrWhiteSpace(exterior);
        bool hasInt = !string.IsNullOrWhiteSpace(interior);

        if (!hasExt && !hasInt) return null;
        if (hasExt && !hasInt) return exterior;
        if (!hasExt) return interior;

        return string.Equals(exterior, interior, StringComparison.OrdinalIgnoreCase)
            ? exterior
            : $"ภายนอก: {exterior} / ภายใน: {interior}";
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class BuildingHeaderRow
    {
        public Guid BuildingDetailId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public string? HouseNumber { get; init; }
        public string? BuildingTypeText { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? ModelName { get; init; }
        public string? BuildingName { get; init; }
        public string? OwnerName { get; init; }
        public string? BuiltOnTitleNumber { get; init; }
        public decimal? UsableArea { get; init; }
        public int? BuildingAge { get; init; }
        public string? BuildingCondition { get; init; }
        public string? MaterialQuality { get; init; }
        public string? BuildingForm { get; init; }
        public string? UtilizationType { get; init; }
        public string? UtilizationTypeOther { get; init; }

        // JSON-array columns (raw nvarchar from Dapper)
        public string? StructureType { get; init; }
        public string? StructureTypeOther { get; init; }
        public string? RoofType { get; init; }
        public string? RoofTypeOther { get; init; }
        public string? RoofFrameType { get; init; }
        public string? RoofFrameTypeOther { get; init; }
        public string? CeilingType { get; init; }
        public string? CeilingTypeOther { get; init; }
        public string? InteriorWallType { get; init; }
        public string? InteriorWallTypeOther { get; init; }
        public string? ExteriorWallType { get; init; }
        public string? ExteriorWallTypeOther { get; init; }
        public string? DecorationType { get; init; }
        public string? DecorationTypeOther { get; init; }
        public string? ConstructionType { get; init; }
        public string? ConstructionTypeOther { get; init; }
        public string? Remark { get; init; }
    }

    private sealed class DepreciationRow
    {
        public Guid BuildingAppraisalDetailId { get; init; }
        public string? AreaDescription { get; init; }
        public decimal Area { get; init; }
        public decimal PricePerSqMBeforeDepreciation { get; init; }
        public decimal PriceBeforeDepreciation { get; init; }
        public short AgeYears { get; init; }
        public decimal DepreciationYearPct { get; init; }
        public decimal TotalDepreciationPct { get; init; }
        public decimal DepreciationAmount { get; init; }
        public decimal PriceAfterDepreciation { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group { get; init; }
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    private sealed class SurfaceRow
    {
        public Guid BuildingAppraisalDetailId { get; init; }
        public int FromFloorNumber { get; init; }
        public int ToFloorNumber { get; init; }
        public string? FloorType { get; init; }
        public string? FloorStructureType { get; init; }
        public string? FloorStructureTypeOther { get; init; }
        public string? FloorSurfaceType { get; init; }
        public string? FloorSurfaceTypeOther { get; init; }
    }
}
