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
                  'FloorMaterial',
                  'BuildingForm',
                  'ConstructionMaterial',
                  'RoofType',
                  'RoadSurface',
                  'PublicUtility',
                  'BathroomMaterial'
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
                cad.SubDistrict,
                cad.District,
                cad.Province,
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
            """;

        List<ParamRow> paramRows;
        List<CondoDetailRow> detailRows;
        List<AreaDetailRow> areaRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, appraisalParams))
        {
            // RS01: QParam — parameter translations
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS02: QCond1 — condo detail rows
            detailRows = (await multi.ReadAsync<CondoDetailRow>()).ToList();

            // RS03: QCond2 — area detail rows
            areaRows = (await multi.ReadAsync<AreaDetailRow>()).ToList();
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

        // Total usable area for LandAreaText (sum of all units' TotalArea)
        decimal totalUsable = units.Sum(u => u.TotalArea ?? 0m);
        string? landAreaText = totalUsable > 0
            ? $"{totalUsable:0.##} ตารางเมตร"
            : null;

        // Distance display
        string? distanceText = first.DistanceFromMainRoad.HasValue
            ? $"{first.DistanceFromMainRoad:0.##} เมตร"
            : null;

        // PublicUtilityType is a JSON-serialised List<string>
        string? publicUtility = DecodeJsonArray(first.PublicUtilityType, first.PublicUtilityTypeOther);

        // RoofType is a JSON-serialised List<string>
        string? roofType = DecodeJsonArrayWithTranslation(
            first.RoofType, first.RoofTypeOther, paramMap, "RoofType");

        // Code → display translations for scalar code columns
        string? floorMaterial = TranslateWithOther(
            first.GroundFloorMaterialType, first.GroundFloorMaterialTypeOther, paramMap, "FloorMaterial");

        string? sanitary = TranslateWithOther(
            first.BathroomFloorMaterialType, first.BathroomFloorMaterialTypeOther, paramMap, "BathroomMaterial");

        string? buildingForm = TranslateCode(first.BuildingFormType, paramMap, "BuildingForm");

        string? constructionMaterial = TranslateCode(first.ConstructionMaterialType, paramMap, "ConstructionMaterial");

        string? roadSurfaceType = TranslateWithOther(
            first.RoadSurfaceType, first.RoadSurfaceTypeOther, paramMap, "RoadSurface");

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
            Remark                = first.Remark
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
}
