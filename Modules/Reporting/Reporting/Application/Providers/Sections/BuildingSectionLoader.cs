using System.Data;
using Reporting.Application.Models.Sections;
using Shared.DDD;

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
                bad.IsAppraisable,
                bad.NotAppraisableReason,
                bad.IsUnderConstruction,
                -- Construction progress: ConstructionInspections.SummaryCurrentProgressPct holds the inspection-level result in BOTH modes
                -- (ConstructionInspection.RecomputeSummary; see the header of vw_AppraisalConstructionProgress), so nothing branches on
                -- IsFullDetail. Clamped to 0-100 like that view's displayed figure. NULL (never a made-up 0%) when the building has no inspection
                -- or no figure was ever keyed. ConstructionInspections is unique per property (IX_ConstructionInspections_AppraisalPropertyId),
                -- so the LEFT JOIN never repeats a building.
                CASE WHEN ci.SummaryCurrentProgressPct < 0   THEN 0
                     WHEN ci.SummaryCurrentProgressPct > 100 THEN 100
                     ELSE ci.SummaryCurrentProgressPct END
                                              AS ConstructionProgressPct,
                bad.ModelName,
                bad.PropertyName              AS BuildingName,
                bad.OwnerName,
                bad.IsOwnerVerified,
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
                bad.BuildingWidth,
                bad.BuildingLength,
                bad.SpaceDivision,
                bad.MaintenanceType,
                bad.MaintenanceTypeOther,
                bad.DoorType,
                bad.DoorTypeOther,
                bad.WindowType,
                bad.WindowTypeOther,
                bad.SanitaryType,
                bad.SanitaryTypeOther,
                bad.BuildingSystemType,
                bad.BuildingSystemTypeOther,
                bad.ConstructionLicenseNumber         AS LicenseNumber,
                bad.ConstructionLicenseExpirationDate AS LicenseExpiration,
                bad.BuildingInsurancePrice            AS InsurancePrice,
                -- The cost table is only a reference when the building cost was not part of the group's value: the group's
                -- selected Cost approach must hold a selected BuildingCost method (or, when it selects none, a BuildingCost
                -- that carries a value — the rule PricingMethodPicker applies). A selected Cost approach whose only selected
                -- method is a land+building WQS already values the building; this table is then just for reference.
                -- An ungrouped building has no pricing context, so it is not labelled.
                CAST(CASE WHEN pg.Id IS NULL THEN 0
                          WHEN EXISTS (SELECT 1 FROM appraisal.PricingAnalysis pa
                                       JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
                                       JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
                                       WHERE pa.AnchorId = pg.Id AND pa.SubjectType = 0
                                         AND paa.ApproachType = 'Cost' AND paa.IsSelected = 1
                                         AND pam.MethodType = 'BuildingCost'
                                         AND (pam.IsSelected = 1
                                              OR (pam.MethodValue IS NOT NULL
                                                  AND NOT EXISTS (SELECT 1 FROM appraisal.PricingAnalysisMethods p2
                                                                  WHERE p2.ApproachId = paa.Id AND p2.IsSelected = 1))))
                          THEN 0 ELSE 1 END AS bit)       AS CostIsReference,
                (SELECT TOP 1 paa.ApproachValue
                 FROM appraisal.PricingAnalysis pa
                 JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
                 WHERE pa.AnchorId = pg.Id AND pa.SubjectType = 0 AND paa.ApproachType = 'Cost'
                 ORDER BY paa.Id)                         AS CostApproachValue,
                (SELECT SUM(pam.MethodValue)
                 FROM appraisal.PricingAnalysis pa
                 JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
                 JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
                 WHERE pa.AnchorId = pg.Id AND pa.SubjectType = 0 AND paa.ApproachType = 'Cost'
                   AND pam.IsSelected = 1 AND pam.Role = 'Land') AS CostLandValue,
                -- หมายเหตุและสมมติฐาน of the group's BuildingCost method (the selected one first).
                (SELECT TOP 1 LTRIM(RTRIM(pam.Remark))
                 FROM appraisal.PricingAnalysis pa
                 JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
                 JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
                 WHERE pa.AnchorId = pg.Id AND pa.SubjectType = 0 AND pam.MethodType = 'BuildingCost'
                   AND NULLIF(LTRIM(RTRIM(pam.Remark)), '') IS NOT NULL
                 ORDER BY paa.IsSelected DESC, pam.IsSelected DESC, pam.Id) AS CostMethodRemark,
                bad.Remark
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            LEFT JOIN appraisal.ConstructionInspections ci ON ci.AppraisalPropertyId = ap.Id
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
                bdd.Id                         AS DetailId,
                bdd.BuildingAppraisalDetailId,
                bdd.DepreciationMethod,
                bdd.PricePerSqMAfterDepreciation,
                bdd.AreaDescription,
                bdd.Area,
                bdd.PricePerSqMBeforeDepreciation,
                bdd.PriceBeforeDepreciation,
                bdd.Year                       AS AgeYears,
                bdd.DepreciationYearPct,
                bdd.TotalDepreciationPct,
                bdd.PriceDepreciation          AS DepreciationAmount,
                bdd.PriceAfterDepreciation,
                bdd.IsBuilding,
                bad.BuildingCostValue
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
                  'Decoration', 'ConstructionType', 'FloorType', 'FloorStructure', 'FloorSurface',
                  'BuildingMaintenance', 'Door', 'Window', 'Sanitary', 'BuildingSystem');

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

            -- RS05: ONE photo per building — the thumbnail, else the lowest SequenceNumber (LinkedAt, then Id break ties;
            -- v7 Guids are not time-ordered so Id alone is not a ranking). File/mime resolve as AppendixSectionLoader does:
            -- document.Documents first, the denormalized gallery columns only as fallback. Images only.
            SELECT BuildingDetailId, FilePath
            FROM (
                SELECT bad.Id AS BuildingDetailId,
                       COALESCE(d.StoragePath, g.FilePath) AS FilePath,
                       ROW_NUMBER() OVER (PARTITION BY bad.Id
                                          ORDER BY ppm.IsThumbnail DESC, ppm.SequenceNumber, ppm.LinkedAt, ppm.Id) AS Rn
                FROM appraisal.BuildingAppraisalDetails bad
                JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                JOIN appraisal.PropertyPhotoMappings ppm ON ppm.AppraisalPropertyId = ap.Id
                JOIN appraisal.AppraisalGallery g ON g.Id = ppm.GalleryPhotoId
                LEFT JOIN document.Documents d ON d.Id = g.DocumentId
                WHERE ap.AppraisalId = @AppraisalId
                  -- images only, BEFORE the ranking: a PDF ranked first must not take the building's photo slot
                  AND COALESCE(d.MimeType, g.MimeType) LIKE 'image/%'
                  AND COALESCE(d.StoragePath, g.FilePath) IS NOT NULL
                  -- the gallery's own FilePath is only a fallback for a photo with no document row at all (d is the unfiltered join):
                  -- a photo whose document was deleted or deactivated is gone, and must not come back through the denormalised path
                  AND (d.Id IS NULL OR (d.IsActive = 1 AND d.IsDeleted = 0))
            ) x
            WHERE Rn = 1;

            -- RS06: depreciation rate by year range, per cost row (Gross mode = one row 1..age).
            SELECT bdp.BuildingDepreciationDetailId AS DetailId, bdp.AtYear, bdp.ToYear, bdp.DepreciationPerYear
            FROM appraisal.BuildingDepreciationPeriods bdp
            JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.Id = bdp.BuildingDepreciationDetailId
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY bdp.BuildingDepreciationDetailId, bdp.AtYear, bdp.ToYear;
            """;

        List<BuildingHeaderRow> headerRows;
        List<DepreciationRow> rawRows;
        List<ParamRow> paramRows;
        List<SurfaceRow> surfaceRows;
        List<PhotoRow> photoRows;
        ILookup<Guid, DepreciationPeriodRow> periodsByDetail;

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

            // RS05 — one photo per building
            photoRows = (await multi.ReadAsync<PhotoRow>()).ToList();

            // RS06 — depreciation periods per cost row
            periodsByDetail = (await multi.ReadAsync<PeriodRow>()).ToLookup(
                r => r.DetailId, r => new DepreciationPeriodRow { AtYear = r.AtYear, ToYear = r.ToYear, DepreciationPerYear = r.DepreciationPerYear });
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
            var codes = AppraisalSummaryCommonLoader.OrderCodes(ParseJsonArray(raw));
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

        var photoByBuilding = photoRows.ToDictionary(r => r.BuildingDetailId, r => AppendixSectionLoader.ToFileUri(r.FilePath));

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
                .Select((r, i) =>
                {
                    var periods = periodsByDetail[r.DetailId].ToList();
                    return new BuildingCostRow
                    {
                        Sequence = i + 1,
                        AreaItemName = r.AreaDescription,
                        Area = r.Area == 0 ? null : r.Area,
                        PricePerSqm = r.PricePerSqMBeforeDepreciation == 0 ? null : r.PricePerSqMBeforeDepreciation,
                        Price = r.PriceBeforeDepreciation == 0 ? null : r.PriceBeforeDepreciation,
                        AgeYears = r.AgeYears == 0 ? null : r.AgeYears,
                        DepreciationPctPerYear = PerYearPct(r.DepreciationMethod, r.DepreciationYearPct, periods),
                        TotalDepreciationPct = r.TotalDepreciationPct == 0 ? null : r.TotalDepreciationPct,
                        DepreciationAmount = r.DepreciationAmount == 0 ? null : r.DepreciationAmount,
                        ValueAfterDepreciation = r.PriceAfterDepreciation == 0 ? null : r.PriceAfterDepreciation,
                        DepreciationMethod = DepreciationMethodLabel(r.DepreciationMethod),
                        PricePerSqmAfterDepreciation = r.PricePerSqMAfterDepreciation == 0 ? null : r.PricePerSqMAfterDepreciation,
                        Periods = periods,
                    };
                })
                .ToList();

            // Totals (computed in C# to avoid a second SQL round-trip)
            decimal? totalArea = deps.Count > 0 ? deps.Sum(r => r.Area) : null;
            decimal? totalPrice = deps.Count > 0 ? deps.Sum(r => r.PriceBeforeDepreciation) : null;
            // The table closes on the Building Cost Value the appraiser saw on the property form:
            // their keyed figure when they entered one, otherwise the depreciated sum rounded to the
            // nearest 1,000. The rows above keep their own exact figures — this is the line the rest of
            // the appraisal reads off, so it has to agree with the form and with the summary report.
            // KEEP IN SYNC with AppraisalSummaryLandBuildingDataProvider's buildingValueById.
            decimal? rowsTotal = deps.Count == 0 ? null : RoundedRowsTotal(deps.Sum(r => r.PriceAfterDepreciation));
            decimal? totalValueAfterDepr = deps.Count == 0 ? null : deps[0].BuildingCostValue ?? rowsTotal;
            // The building page prints the rows' own total, and the keyed figure beside it only when it differs
            // (the same "Edited" test the valuation page applies to the group value).
            decimal? costOverride = rowsTotal is { } computed
                ? AppraisalSummaryLandBuildingDataProvider.BuildingOverride(deps[0].BuildingCostValue, computed)
                : null;

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

            // ส่วนพัฒนา = every non-building row; a property whose rows are ALL non-building is development-only
            // (the same rule as the summary report and the brief — Shared.DDD.DevelopmentOnlyBuilding).
            var devOnly = DevelopmentOnlyBuilding.IsDevelopmentOnly(deps.Select(r => r.IsBuilding));
            var devItems = deps.Where(r => !r.IsBuilding)
                .Select((r, i) => new DevelopmentItem
                {
                    AreaDescription = r.AreaDescription,
                    BuildingTypeText = hdr.BuildingTypeText,
                    Area = r.Area == 0 ? null : r.Area,
                    AgeYears = r.AgeYears == 0 ? null : r.AgeYears,
                    // The property's remark belongs to the property: print it once, on its first item.
                    Remark = devOnly && i == 0 ? AppraisalSummaryCommonLoader.FirstNonBlank(hdr.Remark) : null,
                    IsAppraisable = hdr.IsAppraisable,
                    NotAppraisableReason = PlaceholderText.Stated(hdr.NotAppraisableReason),
                })
                .ToList();

            sections.Add(new BuildingSection
            {
                IsDevelopmentOnly = devOnly,
                DevelopmentItems = devItems,
                GroupNumber = hdr.GroupNumber,
                GroupName = hdr.GroupName,
                PhotoSrc = photoByBuilding.GetValueOrDefault(hdr.BuildingDetailId),
                HouseNumber = hdr.HouseNumber,
                IsAppraisable = hdr.IsAppraisable,
                NotAppraisableReason = PlaceholderText.Stated(hdr.NotAppraisableReason),
                BuildingTypeText = hdr.BuildingTypeText,
                NumberOfFloors = hdr.NumberOfFloors,
                ModelName = hdr.ModelName,
                BuildingName = hdr.BuildingName,
                OwnerName = hdr.OwnerName,   // the raw name; BuildingSection.OwnerText applies the not-verified wording once
                IsOwnerVerified = hdr.IsOwnerVerified,
                IsUnderConstruction = hdr.IsUnderConstruction,
                ConstructionProgressPct = hdr.ConstructionProgressPct,
                BuiltOnTitleNumber = hdr.BuiltOnTitleNumber,
                LicenseSource = null,   // no source
                LicenseNumber = AppraisalSummaryCommonLoader.FirstNonBlank(hdr.LicenseNumber),
                LicenseExpiration = hdr.LicenseExpiration,
                InsurancePrice = hdr.InsurancePrice,
                CostIsReference = hdr.CostIsReference,
                CostApproachValue = hdr.CostApproachValue,
                CostLandValue = hdr.CostLandValue,
                CostMethodRemark = hdr.CostMethodRemark,
                UsableArea = hdr.UsableArea,
                SizeText = hdr.BuildingWidth is null && hdr.BuildingLength is null
                    ? null
                    : $"{FormatMetres(hdr.BuildingWidth)} x {FormatMetres(hdr.BuildingLength)}",
                SpaceDivision = AppraisalSummaryCommonLoader.FirstNonBlank(hdr.SpaceDivision),
                BuildingAge = hdr.BuildingAge,
                BuildingCondition = hdr.BuildingCondition,
                Maintenance = ResolveCodes("BuildingMaintenance", hdr.MaintenanceType, hdr.MaintenanceTypeOther),
                MaterialQuality = ResolveCodes("BuildingMaterial", hdr.MaterialQuality),
                BuildingForm = ResolveCodes("BuildingStyle", hdr.BuildingForm),
                Utilization = ResolveCodes("Utilization", hdr.UtilizationType, hdr.UtilizationTypeOther),
                MainStructure = ResolveCodes("GeneralStructure", hdr.StructureType, hdr.StructureTypeOther),
                RoofFrame = ResolveCodes("RoofFrame", hdr.RoofFrameType, hdr.RoofFrameTypeOther),
                RoofMaterial = ResolveCodes("Roof", hdr.RoofType, hdr.RoofTypeOther),
                Wall = wall,
                Ceiling = ResolveCodes("Ceiling", hdr.CeilingType, hdr.CeilingTypeOther),
                Painting = ResolveCodes("Decoration", hdr.DecorationType, hdr.DecorationTypeOther),
                Door = ResolveCodes("Door", hdr.DoorType, hdr.DoorTypeOther),
                Window = ResolveCodes("Window", hdr.WindowType, hdr.WindowTypeOther),
                Sanitary = ResolveCodes("Sanitary", hdr.SanitaryType, hdr.SanitaryTypeOther),
                BuildingSystems = ResolveCodes("BuildingSystem", hdr.BuildingSystemType, hdr.BuildingSystemTypeOther),
                Decoration = ResolveCodes("ConstructionType", hdr.ConstructionType, hdr.ConstructionTypeOther),
                Floors = floors,
                CostRows = costRows,
                TotalArea = totalArea == 0 ? null : totalArea,
                TotalPrice = totalPrice == 0 ? null : totalPrice,
                TotalValueAfterDepreciation = totalValueAfterDepr == 0 ? null : totalValueAfterDepr,
                CostRowsTotal = rowsTotal == 0 ? null : rowsTotal,
                CostOverride = costOverride,
                Remark = AppraisalSummaryCommonLoader.FirstNonBlank(hdr.Remark),
            });
        }

        return sections;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>The depreciated rows' sum rounded to the nearest 1,000 — the Building Cost Value the form computes when nothing is keyed.</summary>
    internal static decimal RoundedRowsTotal(decimal sum) => Math.Round(sum / 1000m, MidpointRounding.AwayFromZero) * 1000m;

    /// <summary>
    /// ค่าเสื่อม ปีละ %. รายช่วง (Period) has one rate per year range → null here, the rates print in the period table
    /// under the cost table. เหมารวม (Gross): the form leaves the DepreciationYearPct column 0 and keeps the rate on its
    /// one BuildingDepreciationPeriods row (years 1..age) → the column when set, else that row's rate (0 included);
    /// nothing stored anywhere → null.
    /// </summary>
    internal static decimal? PerYearPct(string? method, decimal storedYearPct, IReadOnlyList<DepreciationPeriodRow> periods) =>
        string.Equals(method?.Trim(), "Period", StringComparison.OrdinalIgnoreCase) ? null
        : storedYearPct != 0 ? storedYearPct
        : periods.Count == 1 ? periods[0].DepreciationPerYear
        : null;

    /// <summary>The screen's labels (costBuilding.table.methodGross / methodPeriod).</summary>
    internal static string? DepreciationMethodLabel(string? method) => method?.Trim() switch
    {
        "Gross" => "เหมารวม",
        "Period" => "รายช่วง",
        null or "" => null,
        var other => other
    };

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

    /// <summary>A width / length in metres, trimmed to 2 decimals; "-" when not entered.</summary>
    private static string FormatMetres(decimal? v) =>
        v is null ? "-" : v.Value.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);

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

    private sealed class PhotoRow
    {
        public Guid BuildingDetailId { get; init; }
        public string? FilePath { get; init; }
    }

    private sealed class BuildingHeaderRow
    {
        public string? LicenseNumber { get; init; }
        public DateTime? LicenseExpiration { get; init; }
        public decimal? InsurancePrice { get; init; }
        public string? CostMethodRemark { get; init; }
        public bool CostIsReference { get; init; }
        public decimal? CostApproachValue { get; init; }
        public decimal? CostLandValue { get; init; }
        public Guid BuildingDetailId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public string? HouseNumber { get; init; }
        public bool? IsAppraisable { get; init; }
        public string? NotAppraisableReason { get; init; }
        public bool? IsUnderConstruction { get; init; }
        public decimal? ConstructionProgressPct { get; init; }
        public string? BuildingTypeText { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? ModelName { get; init; }
        public string? BuildingName { get; init; }
        public string? OwnerName { get; init; }
        public bool? IsOwnerVerified { get; init; }
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
        public decimal? BuildingWidth { get; init; }
        public decimal? BuildingLength { get; init; }
        public string? SpaceDivision { get; init; }
        public string? MaintenanceType { get; init; }
        public string? MaintenanceTypeOther { get; init; }
        public string? DoorType { get; init; }
        public string? DoorTypeOther { get; init; }
        public string? WindowType { get; init; }
        public string? WindowTypeOther { get; init; }
        public string? SanitaryType { get; init; }
        public string? SanitaryTypeOther { get; init; }
        public string? BuildingSystemType { get; init; }
        public string? BuildingSystemTypeOther { get; init; }
        public string? Remark { get; init; }
    }

    private sealed class PeriodRow
    {
        public Guid DetailId { get; init; }
        public int AtYear { get; init; }
        public int ToYear { get; init; }
        public decimal DepreciationPerYear { get; init; }
    }

    private sealed class DepreciationRow
    {
        public Guid DetailId { get; init; }
        public string? DepreciationMethod { get; init; }
        public decimal PricePerSqMAfterDepreciation { get; init; }
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
        public bool IsBuilding { get; init; }

        /// <summary>
        /// The appraiser's keyed Building Cost Value for the owning property, repeated on every row
        /// of that property. Null = use the depreciated sum.
        /// </summary>
        public decimal? BuildingCostValue { get; init; }
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
