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
    public static async Task<BuildingSection?> LoadAsync(
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
            -- RS01: Step 1 — Resolve first building AppraisalProperty
            SELECT TOP 1
                ap.Id AS PropertyId
            FROM appraisal.AppraisalProperties ap
            WHERE ap.AppraisalId = @AppraisalId
              AND ap.PropertyType = 'Building'
            ORDER BY ap.SequenceNumber, ap.Id;

            -- RS02: Step 2 — Building header + parameter translations
            SELECT
                bad.Id                        AS BuildingDetailId,
                bad.HouseNumber,
                bad.BuildingType,
                bad.BuildingTypeOther,
                COALESCE(ptBT.Description, bad.BuildingTypeOther, bad.BuildingType)
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
                COALESCE(ptBC.Description, bad.BuildingConditionTypeOther, bad.BuildingConditionType)
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
            WHERE bad.AppraisalPropertyId = (
                SELECT TOP 1 ap2.Id
                FROM appraisal.AppraisalProperties ap2
                WHERE ap2.AppraisalId  = @AppraisalId
                  AND ap2.PropertyType = 'Building'
                ORDER BY ap2.SequenceNumber, ap2.Id);

            -- RS03: Step 3 — Depreciation/cost rows
            -- Derives BuildingAppraisalDetailId via double-hop subquery through @AppraisalId.
            SELECT
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
            WHERE bdd.BuildingAppraisalDetailId = (
                SELECT TOP 1 bad2.Id
                FROM appraisal.BuildingAppraisalDetails bad2
                JOIN appraisal.AppraisalProperties ap3
                    ON ap3.Id = bad2.AppraisalPropertyId
                WHERE ap3.AppraisalId  = @AppraisalId
                  AND ap3.PropertyType = 'Building'
                ORDER BY ap3.SequenceNumber, ap3.Id)
            ORDER BY bdd.Id;
            """;

        Guid? propertyId;
        BuildingHeaderRow? hdr;
        List<DepreciationRow> rawRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, appraisalParams))
        {
            // RS01: Step 1 — property id (used only for the null-guard below)
            propertyId = await multi.ReadFirstOrDefaultAsync<Guid?>();

            // RS02: Step 2 — building header
            hdr = await multi.ReadFirstOrDefaultAsync<BuildingHeaderRow>();

            // RS03: Step 3 — depreciation rows (always read to drain the result set)
            rawRows = (await multi.ReadAsync<DepreciationRow>()).ToList();
        }

        if (propertyId is null)
            return null;

        if (hdr is null)
            return null;

        // Map to public model rows (1-based sequence assigned here)
        var costRows = rawRows
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
        decimal? totalArea = rawRows.Count > 0 ? rawRows.Sum(r => r.Area) : null;
        decimal? totalPrice = rawRows.Count > 0 ? rawRows.Sum(r => r.PriceBeforeDepreciation) : null;
        decimal? totalValueAfterDepr = rawRows.Count > 0 ? rawRows.Sum(r => r.PriceAfterDepreciation) : null;

        // ── Flatten JSON-array columns stored as nvarchar ─────────────────────────
        // StructureType, RoofType, CeilingType, InteriorWallType, ExteriorWallType are
        // serialised as JSON arrays by EF Core (see BuildingAppraisalDetailConfiguration).
        // Dapper reads them back as raw JSON strings; join with comma for display.
        string? FlattenJson(string? raw, string? other)
        {
            var items = ParseJsonArray(raw);
            if (!string.IsNullOrWhiteSpace(other))
                items.Add(other.Trim());
            return items.Count > 0 ? string.Join(", ", items) : null;
        }

        string? wall = BuildWallText(
            FlattenJson(hdr.ExteriorWallType, hdr.ExteriorWallTypeOther),
            FlattenJson(hdr.InteriorWallType, hdr.InteriorWallTypeOther));

        string? utilization = string.IsNullOrWhiteSpace(hdr.UtilizationType)
            ? hdr.UtilizationTypeOther
            : !string.IsNullOrWhiteSpace(hdr.UtilizationTypeOther)
                ? $"{hdr.UtilizationType}, {hdr.UtilizationTypeOther}"
                : hdr.UtilizationType;

        string? decoration = string.IsNullOrWhiteSpace(hdr.ConstructionType)
            ? hdr.ConstructionTypeOther
            : !string.IsNullOrWhiteSpace(hdr.ConstructionTypeOther)
                ? $"{hdr.ConstructionType}, {hdr.ConstructionTypeOther}"
                : hdr.ConstructionType;

        string? painting = string.IsNullOrWhiteSpace(hdr.DecorationType)
            ? hdr.DecorationTypeOther
            : !string.IsNullOrWhiteSpace(hdr.DecorationTypeOther)
                ? $"{hdr.DecorationType}, {hdr.DecorationTypeOther}"
                : hdr.DecorationType;

        return new BuildingSection
        {
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
            MaterialQuality = hdr.MaterialQuality,
            BuildingForm = hdr.BuildingForm,
            Utilization = utilization,
            MainStructure = FlattenJson(hdr.StructureType, hdr.StructureTypeOther),
            RoofMaterial = FlattenJson(hdr.RoofType, hdr.RoofTypeOther),
            Wall = wall,
            Ceiling = FlattenJson(hdr.CeilingType, hdr.CeilingTypeOther),
            Painting = painting,
            Door = null,            // no source
            Window = null,          // no source
            Sanitary = null,        // no source
            Decoration = decoration,
            CostRows = costRows,
            TotalArea = totalArea == 0 ? null : totalArea,
            TotalPrice = totalPrice == 0 ? null : totalPrice,
            TotalValueAfterDepreciation = totalValueAfterDepr == 0 ? null : totalValueAfterDepr,
            Remark = hdr.Remark,
        };
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
}
