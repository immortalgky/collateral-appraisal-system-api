using System.Data;
using System.Text.Json;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the <see cref="CondoSection"/> for FSD §2.1.2.3 "รายละเอียดห้องชุด".
///
/// Read-only Dapper queries only; no EF Core tracking.
/// Returns <see langword="null"/> when the appraisal has no condo properties.
///
/// Queries:
///   QCond1  appraisal.CondoAppraisalDetails + AppraisalProperties
///           — first-property header fields + code columns
///   QCond2  appraisal.CondoAppraisalAreaDetails per CondoAppraisalDetail
///           — pivoted into InteriorArea / BalconyArea / AirConArea / OtherArea
///   QParam  parameter.Parameters (group 'TH', relevant material/type groups)
///           — code→Thai translation for display strings
/// </summary>
internal static class CondoSectionLoader
{
    // ── Area-description keyword matching ─────────────────────────────────────────

    private static bool IsInterior(string? d) =>
        d is not null &&
        (d.Contains("ภายใน", StringComparison.OrdinalIgnoreCase) ||
         d.Contains("Interior", StringComparison.OrdinalIgnoreCase));

    private static bool IsBalcony(string? d) =>
        d is not null &&
        (d.Contains("ระเบียง", StringComparison.OrdinalIgnoreCase) ||
         d.Contains("Balcony", StringComparison.OrdinalIgnoreCase));

    private static bool IsAirCon(string? d) =>
        d is not null &&
        (d.Contains("แอร์", StringComparison.OrdinalIgnoreCase) ||
         d.Contains("AirCond", StringComparison.OrdinalIgnoreCase));

    // ── Public entry point ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the condo section for <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when the appraisal has no condo properties.
    /// </summary>
    public static async Task<CondoSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var appraisalParams = new DynamicParameters();
        appraisalParams.Add("AppraisalId", appraisalId);

        // ── Batch: 3 result sets, single round-trip ───────────────────────────────
        //
        // QParam (RS01) — parameter.Parameters for code→display translation.
        //   Groups: FloorMaterial, BuildingForm, ConstructionMaterial, RoofType,
        //           RoadSurface, PublicUtility, BathroomMaterial.
        //   Not scoped to @AppraisalId — fetches all relevant param rows once.
        //
        // QCond1 (RS02) — all CondoAppraisalDetails rows for the appraisal.
        //   Ordered by ap.SequenceNumber.
        //   Deferred (no source): WallMaterialType, CeilingMaterialType,
        //   DoorMaterialType, WindowMaterialType, UrbanPlanningType, LandZoneType,
        //   RoadZoneWidth/RoadFrontage.
        //
        // QCond2 (RS03) — all CondoAppraisalAreaDetails rows for the appraisal.
        //   FK column: "CondoAppraisalDetailsId"
        //   (CondoAppraisalDetailConfiguration HasForeignKey("CondoAppraisalDetailsId"))
        //
        // All three statements compile without @AppraisalId being used by RS01 —
        // DynamicParameters ignores unused parameters.
        const string batchSql = """
            -- RS01: QParam — parameter translations
            SELECT [Group], [Code], [Description]
            FROM parameter.Parameters
            WHERE [Language] = 'TH'
              AND IsActive = 1
              AND [Group] IN (
                  'GroundFlooringMaterials',
                  'BuildingForm',
                  'ConstructionMaterials',
                  'Roof',
                  'RoadSurface',
                  'PublicUtility',
                  'BathroomFlooringMaterials'
              );

            -- RS02: QCond1 — condo detail rows (all properties, ordered by sequence)
            SELECT
                cad.Id,
                ap.SequenceNumber,
                cad.RoomNumber,
                cad.FloorNumber,
                cad.NumberOfFloors,
                cad.CondoName,
                cad.BuildingNumber,
                cad.BuiltOnTitleNumber,
                cad.CondoRegistrationNumber,
                cad.UsableArea,
                cad.OwnerName,
                cad.ObligationDetails,
                cad.Street                   AS Road,
                cad.Soi,
                cad.DistanceFromMainRoad,
                cad.AccessRoadWidth,
                cad.RoadSurfaceType,
                cad.RoadSurfaceTypeOther,
                cad.PublicUtilityType,
                cad.PublicUtilityTypeOther,
                COALESCE(tsub.NameTh,  cad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, cad.District)    AS District,
                COALESCE(tprov.NameTh, cad.Province)    AS Province,
                cad.GroundFloorMaterialType,
                cad.GroundFloorMaterialTypeOther,
                cad.BathroomFloorMaterialType,
                cad.BathroomFloorMaterialTypeOther,
                cad.BuildingFormType,
                cad.ConstructionMaterialType,
                cad.RoofType,
                cad.RoofTypeOther,
                cad.BuildingAge,
                cad.IsInExpropriationLine,
                cad.Remark
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = cad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = cad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = cad.SubDistrict
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS03: QCond2 — area detail rows for all condo units
            SELECT
                cada.CondoAppraisalDetailsId,
                cada.AreaDescription,
                cada.AreaSize
            FROM appraisal.CondoAppraisalAreaDetails cada
            JOIN appraisal.CondoAppraisalDetails cad ON cad.Id = cada.CondoAppraisalDetailsId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId;

            -- RS04: QPrice — selected pricing for the property GROUP(S) the condo belongs to.
            --   The condo appraisal price comes from its group's value, NOT the application total.
            --   Scope: only groups linked to a CondoAppraisalDetails row (via PropertyGroupItems).
            --   Mirrors the Q9 join in AppraisalSummaryCommonLoader: selected
            --   PricingAnalysis → selected Approach → selected Method → PricingFinalValue.
            --   UnitType drives the per-unit-vs-per-sqm display rule.
            SELECT DISTINCT
                pg.Id AS GroupId,
                m.UnitType,
                m.ValuePerUnit,
                m.MethodValue,
                COALESCE(pa.FinalAppraisedValue, m.FinalValueRounded, m.AppraisalPrice) AS GroupValue
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PropertyGroupItems pgi
                ON pgi.PropertyGroupId = pg.Id
            JOIN appraisal.CondoAppraisalDetails cad
                ON cad.AppraisalPropertyId = pgi.AppraisalPropertyId
            LEFT JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            OUTER APPLY (
                SELECT TOP 1
                    pm.UnitType, pm.ValuePerUnit, pm.MethodValue,
                    fv.FinalValueRounded, fv.AppraisalPrice
                FROM appraisal.PricingAnalysisApproaches pap
                JOIN appraisal.PricingAnalysisMethods pm
                    ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                LEFT JOIN appraisal.PricingFinalValues fv
                    ON fv.PricingMethodId = pm.Id
                WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
            ) m
            WHERE pg.AppraisalId = @AppraisalId;
            """;

        List<ParamRow> paramRows;
        List<CondoDetailRow> detailRows;
        List<AreaDetailRow> areaRows;
        List<PricingRow> pricingRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, appraisalParams))
        {
            // RS01: QParam — parameter translations
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS02: QCond1 — condo detail rows
            detailRows = (await multi.ReadAsync<CondoDetailRow>()).ToList();

            // RS03: QCond2 — area detail rows
            areaRows = (await multi.ReadAsync<AreaDetailRow>()).ToList();

            // RS04: QPrice — selected pricing per group
            pricingRows = (await multi.ReadAsync<PricingRow>()).ToList();
        }

        if (detailRows.Count == 0)
            return null;

        // Build lookup: group → (code → description)
        var paramMap = paramRows
            .Where(p => p.Group is not null && p.Code is not null)
            .GroupBy(p => p.Group!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(inner => inner.Key, inner => inner.First().Description,
                        StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var areaByDetailId = areaRows
            .GroupBy(r => r.CondoAppraisalDetailsId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Build unit rows ───────────────────────────────────────────────────────
        var units = new List<CondoUnitAreaRow>(detailRows.Count);
        int seq = 0;

        foreach (var d in detailRows)
        {
            seq++;
            areaByDetailId.TryGetValue(d.Id, out var areas);
            areas ??= [];

            decimal interior = areas.Where(a => IsInterior(a.AreaDescription)).Sum(a => a.AreaSize ?? 0m);
            decimal balcony  = areas.Where(a => IsBalcony(a.AreaDescription)).Sum(a => a.AreaSize ?? 0m);
            decimal airCon   = areas.Where(a => IsAirCon(a.AreaDescription)).Sum(a => a.AreaSize ?? 0m);
            decimal other    = areas
                .Where(a => !IsInterior(a.AreaDescription) && !IsBalcony(a.AreaDescription) && !IsAirCon(a.AreaDescription))
                .Sum(a => a.AreaSize ?? 0m);

            decimal total = interior + balcony + airCon + other;

            // Fall back to UsableArea when no area-detail rows exist
            if (areas.Count == 0 && d.UsableArea.HasValue)
                total = d.UsableArea.Value;

            units.Add(new CondoUnitAreaRow
            {
                Sequence     = seq,
                RoomNumber   = d.RoomNumber,
                FloorNumber  = d.FloorNumber,
                InteriorArea = areas.Count > 0 && interior > 0 ? interior : null,
                BalconyArea  = areas.Count > 0 && balcony  > 0 ? balcony  : null,
                AirConArea   = areas.Count > 0 && airCon   > 0 ? airCon   : null,
                OtherArea    = areas.Count > 0 && other    > 0 ? other    : null,
                TotalArea    = total > 0 ? total : null
            });
        }

        // ── Header fields from first condo detail ──────────────────────────────────
        var first = detailRows[0];

        // เนื้อที่ (FSD §2.1.2.3 field 15) = underlying LAND-plot area in ไร่-งาน-ตารางวา,
        // NOT the condo unit's usable m². CondoAppraisalDetails has no land-area column,
        // so render this empty until a proper source exists (do not fall back to unit area).
        string? landAreaText = null;

        // Distance display
        string? distanceText = first.DistanceFromMainRoad.HasValue
            ? $"{first.DistanceFromMainRoad:0.##} เมตร"
            : null;

        // PublicUtilityType is a JSON-serialised List<string> of codes → translate each.
        string? publicUtility = DecodeJsonArrayWithTranslation(
            first.PublicUtilityType, first.PublicUtilityTypeOther, paramMap, "PublicUtility");

        // RoofType is a JSON-serialised List<string>
        string? roofType = DecodeJsonArrayWithTranslation(
            first.RoofType, first.RoofTypeOther, paramMap, "Roof");

        // Code → display translations for scalar code columns
        string? floorMaterial = TranslateWithOther(
            first.GroundFloorMaterialType, first.GroundFloorMaterialTypeOther, paramMap, "GroundFlooringMaterials");

        string? sanitary = TranslateWithOther(
            first.BathroomFloorMaterialType, first.BathroomFloorMaterialTypeOther, paramMap, "BathroomFlooringMaterials");

        string? buildingForm = TranslateCode(first.BuildingFormType, paramMap, "BuildingForm");

        string? constructionMaterial = TranslateCode(first.ConstructionMaterialType, paramMap, "ConstructionMaterials");

        string? roadSurfaceType = TranslateWithOther(
            first.RoadSurfaceType, first.RoadSurfaceTypeOther, paramMap, "RoadSurface");

        // ── Valuation table (รายละเอียดการประเมินมูลค่าทรัพย์สิน ห้องชุด) ──────────────
        // Part A area components = sums across all units; per-sqm rate + amount have no source.
        decimal interiorTotal = units.Sum(u => u.InteriorArea ?? 0m);
        decimal balconyTotal  = units.Sum(u => u.BalconyArea ?? 0m);
        decimal airConTotal   = units.Sum(u => u.AirConArea ?? 0m);
        decimal otherTotal    = units.Sum(u => u.OtherArea ?? 0m);

        // Part B market area = interior + balcony (รวมระเบียง); fall back to total unit area.
        decimal marketArea = interiorTotal + balconyTotal;
        if (marketArea <= 0)
            marketArea = units.Sum(u => u.TotalArea ?? 0m);

        // Aggregate the selected pricing across the appraisal's property groups.
        var pricedRows = pricingRows.Where(p => p.GroupValue.HasValue).ToList();
        decimal marketValue = pricedRows.Sum(p => p.GroupValue!.Value);
        // Priced "by unit" only when every priced group is per-unit → per-sqm shows "-".
        bool pricedByUnit = pricedRows.Count > 0 &&
            pricedRows.All(p => string.Equals(p.UnitType, "Unit", StringComparison.OrdinalIgnoreCase));

        decimal? marketPricePerSqm;
        decimal? marketAmount;
        if (marketValue <= 0)
        {
            marketPricePerSqm = null;
            marketAmount = null;
        }
        else if (pricedByUnit)
        {
            marketPricePerSqm = null;       // renders as "-"
            marketAmount = marketValue;     // lump sum
        }
        else
        {
            // Use the stored per-sqm rate for the single-group case; otherwise derive value ÷ area.
            decimal? storedRate = pricedRows.Count == 1 ? pricedRows[0].ValuePerUnit : null;
            marketPricePerSqm = storedRate
                ?? (marketArea > 0 ? marketValue / marketArea : (decimal?)null);
            marketAmount = marketPricePerSqm.HasValue && marketArea > 0
                ? marketPricePerSqm.Value * marketArea
                : marketValue;
        }

        var valuation = new CondoValuationDetail
        {
            InteriorArea      = interiorTotal > 0 ? interiorTotal : null,
            BalconyArea       = balconyTotal  > 0 ? balconyTotal  : null,
            AirConArea        = airConTotal   > 0 ? airConTotal   : null,
            OtherArea         = otherTotal    > 0 ? otherTotal    : null,
            MarketArea        = marketArea    > 0 ? marketArea    : null,
            MarketPricePerSqm = marketPricePerSqm,
            MarketAmount      = marketAmount,
            TotalRoundedValue = marketValue   > 0 ? marketValue   : null
        };

        return new CondoSection
        {
            Units                 = units,
            CondoName             = first.CondoName,
            BuildingNumber        = first.BuildingNumber,
            RegistrationNumber    = first.CondoRegistrationNumber,
            BuiltOnTitleNumber    = first.BuiltOnTitleNumber,
            SubDistrict           = first.SubDistrict,
            District              = first.District,
            Province              = first.Province,
            LandAreaText          = landAreaText,
            LocatedOnRoad         = first.Road,
            Soi                   = first.Soi,
            DistanceText          = distanceText,
            OwnerName             = first.OwnerName,
            Obligation            = first.ObligationDetails,
            PositionCorrect       = null,           // no source
            FloorMaterial         = floorMaterial,
            Wall                  = null,           // no source
            Ceiling               = null,           // no source
            Door                  = null,           // no source
            Window                = null,           // no source
            Sanitary              = sanitary,
            RoadSurfaceWidth      = first.AccessRoadWidth,
            RoadZoneWidth         = null,           // no source
            RoadSurfaceType       = roadSurfaceType,
            PublicUtility         = publicUtility,
            BuildingAge           = first.BuildingAge,
            BuildingHeightFloors  = first.NumberOfFloors,
            BuildingForm          = buildingForm,
            ConstructionMaterial  = constructionMaterial,
            RoofType              = roofType,
            IsInExpropriationLine = first.IsInExpropriationLine,
            AreaColour            = null,           // no source — UrbanPlanningType only on LandAppraisalDetails
            LandType              = null,           // no source — no LandZoneType on CondoAppraisalDetails
            Remark                = first.Remark,
            Valuation             = valuation
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Translates a code column to Thai display, appending *Other free text when set.
    /// Falls back to the raw code if no translation found.
    /// </summary>
    private static string? TranslateWithOther(
        string? code, string? other,
        Dictionary<string, Dictionary<string, string?>> paramMap, string group)
    {
        var display = TranslateCode(code, paramMap, group);
        if (!string.IsNullOrWhiteSpace(other))
            return string.IsNullOrWhiteSpace(display) ? other : $"{display} ({other})";
        return display;
    }

    /// <summary>Translates a code to the Thai label; returns the raw code when not found.</summary>
    private static string? TranslateCode(
        string? code,
        Dictionary<string, Dictionary<string, string?>> paramMap, string group)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        if (paramMap.TryGetValue(group, out var codeMap) &&
            codeMap.TryGetValue(code, out var desc) &&
            !string.IsNullOrWhiteSpace(desc))
            return desc;
        return code;
    }

    /// <summary>
    /// Decodes a JSON array of codes, joins with comma.
    /// Other free text is appended at the end when set.
    /// </summary>
    private static string? DecodeJsonArray(string? json, string? other = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.IsNullOrWhiteSpace(other) ? null : other;

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json);
            if (items is null or { Count: 0 })
                return string.IsNullOrWhiteSpace(other) ? null : other;

            var parts = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (!string.IsNullOrWhiteSpace(other))
                parts.Add(other!);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
        catch (JsonException)
        {
            // Stored value is a plain string rather than JSON array
            var plain = json.Trim();
            return !string.IsNullOrWhiteSpace(other) ? $"{plain} ({other})" : plain;
        }
    }

    /// <summary>
    /// Decodes a JSON array of codes, translates each code, then joins with comma.
    /// Other free text is appended at the end when set.
    /// </summary>
    private static string? DecodeJsonArrayWithTranslation(
        string? json, string? other,
        Dictionary<string, Dictionary<string, string?>> paramMap, string group)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.IsNullOrWhiteSpace(other) ? null : other;

        List<string>? codes;
        try
        {
            codes = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return DecodeJsonArray(json, other);
        }

        if (codes is null or { Count: 0 })
            return string.IsNullOrWhiteSpace(other) ? null : other;

        var labels = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => TranslateCode(c, paramMap, group) ?? c)
            .ToList();

        if (!string.IsNullOrWhiteSpace(other))
            labels.Add(other!);

        return labels.Count > 0 ? string.Join(", ", labels) : null;
    }

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────────

    private sealed class CondoDetailRow
    {
        public Guid    Id                           { get; init; }
        public int     SequenceNumber               { get; init; }
        public string? RoomNumber                   { get; init; }
        public string? FloorNumber                  { get; init; }
        public decimal? NumberOfFloors              { get; init; }
        public string? CondoName                    { get; init; }
        public string? BuildingNumber               { get; init; }
        public string? BuiltOnTitleNumber           { get; init; }
        public string? CondoRegistrationNumber      { get; init; }
        public decimal? UsableArea                  { get; init; }
        public string? OwnerName                    { get; init; }
        public string? ObligationDetails            { get; init; }
        public string? Road                         { get; init; }
        public string? Soi                          { get; init; }
        public decimal? DistanceFromMainRoad        { get; init; }
        public decimal? AccessRoadWidth             { get; init; }
        public string? RoadSurfaceType              { get; init; }
        public string? RoadSurfaceTypeOther         { get; init; }
        public string? PublicUtilityType            { get; init; }
        public string? PublicUtilityTypeOther       { get; init; }
        public string? SubDistrict                  { get; init; }
        public string? District                     { get; init; }
        public string? Province                     { get; init; }
        public string? GroundFloorMaterialType      { get; init; }
        public string? GroundFloorMaterialTypeOther { get; init; }
        public string? BathroomFloorMaterialType    { get; init; }
        public string? BathroomFloorMaterialTypeOther { get; init; }
        public string? BuildingFormType             { get; init; }
        public string? ConstructionMaterialType     { get; init; }
        public string? RoofType                     { get; init; }
        public string? RoofTypeOther                { get; init; }
        public int?    BuildingAge                  { get; init; }
        public bool?   IsInExpropriationLine        { get; init; }
        public string? Remark                       { get; init; }
    }

    private sealed class AreaDetailRow
    {
        public Guid    CondoAppraisalDetailsId { get; init; }
        public string? AreaDescription         { get; init; }
        public decimal? AreaSize               { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group       { get; init; }
        public string? Code        { get; init; }
        public string? Description { get; init; }
    }

    private sealed class PricingRow
    {
        public Guid     GroupId      { get; init; }
        public string?  UnitType     { get; init; }
        public decimal? ValuePerUnit { get; init; }
        public decimal? MethodValue  { get; init; }
        public decimal? GroupValue   { get; init; }
    }
}
