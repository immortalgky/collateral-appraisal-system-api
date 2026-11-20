using System.Data;
using System.Text.Json;
using Reporting.Application.Formatting;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "รายละเอียดที่ดิน" (Land Details) section model — FSD §2.1.2.4.
///
/// Data sources (Dapper read-only, no EF tracking):
///   Q1  appraisal.LandAppraisalDetails joined via appraisal.AppraisalProperties
///       → header row (all scalar land attributes for the FIRST property by SequenceNumber).
///   Q2  appraisal.LandTitles (FK LandAppraisalDetailId, one row per title deed)
///       → all titles across all land properties for this appraisal, ordered for display.
///
/// Returns <see langword="null"/> when the appraisal has no land properties so the
/// caller / orchestrator can omit the section from the rendered template.
/// </summary>
internal static class LandSectionLoader
{
    /// <summary>
    /// Loads ONE <see cref="LandSection"/> per land-bearing property (L / LB / lease-land)
    /// for the appraisal, each tagged with its property group and carrying its own titles.
    /// Returns an empty list when the appraisal has no land properties.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load land details for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<List<LandSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Batch: 2 result sets, single round-trip ───────────────────────────────
        //
        // Q1 (RS01) — Land detail header (first property, ordered by SequenceNumber).
        // Q2 (RS02) — All land titles across all land properties for this appraisal.
        //             Both queries are fully parameterised on @AppraisalId.
        //
        // Q1 column notes (confirmed from LandAppraisalDetailConfiguration.cs):
        //   OwnerName (nvarchar 200), ObligationDetails (nvarchar 500),
        //   RoadPassInFrontOfLand (nvarchar 200), RoadSurfaceType (nvarchar 100),
        //   CarriagewayWidth / RightOfWayWidth / RoadFrontage (decimal 10,2),
        //   LandEntranceExitType / PlotLocationType / PublicUtilityType /
        //     LandUseType / TransportationAccessType / LandZoneType (nvarchar 500, JSON arrays),
        //   LandDescription / PropertyAnticipationType / UrbanPlanningType (nvarchar),
        //   IsInExpropriationLine (bool) — the legacy IsExpropriated (ระวางเวนคืน) is no longer read,
        //   Remark (nvarchar 4000),
        //   Address VO: SubDistrict/District/Province/LandOffice (HasColumnName).
        //
        // Q2 column notes (confirmed from LandAppraisalDetailConfiguration OwnsMany):
        //   TitleNumber (nvarchar 200), LandParcelNumber (nvarchar 50),
        //   SurveyNumber (nvarchar 50), AreaRai/AreaNgan/AreaSquareWa (decimal via Area VO).
        //   OwnerName pulled from parent LandAppraisalDetails (LandTitles has no OwnerName).
        const string batchSql = """
            -- RS01: Q1 — One row per land-bearing property, with its property group.
            -- Geocodes (sub-district/district/province) resolved to Thai names via the
            -- parameter.Title* tables; falls back to the raw stored code when unmapped.
            -- GroupNumber 0 = property not assigned to any group (ungrouped fallback).
            SELECT
                lad.Id                       AS LandDetailId,
                COALESCE(pg.GroupNumber, 0)  AS GroupNumber,
                pg.GroupName,
                lad.OwnerName,
                lad.ObligationDetails,
                lad.ObligationType,
                lad.MortgageDetails,
                lad.RoadPassInFrontOfLand,
                lad.RoadSurfaceType,
                lad.RoadSurfaceTypeOther,
                lad.CarriagewayWidth,
                lad.RightOfWayWidth,
                lad.RoadFrontage,
                lad.NumberOfLanes,
                lad.NumberOfSidesFacingRoad,
                lad.IsLandLocationVerified,
                lad.LandLocationRemark,
                lad.IsOwnerVerified,
                lad.IsRentedOut,
                lad.Latitude,
                lad.Longitude,
                lad.DistanceFromMainRoad,
                lad.LandFillType,
                lad.LandFillTypeOther,
                lad.LandFillPercent,
                lad.SoilLevel,
                lad.PondArea,
                lad.PondDepth,
                lad.LandAccessibilityType,
                lad.LandAccessibilityRemark,
                lad.HasElectricity            AS FarFromElectricity,
                lad.ElectricityDistance,
                lad.IsForestBoundary,
                lad.ForestBoundaryRemark,
                lad.HasFloodProblem,
                lad.FloodProblemRemark,
                lad.HasPollutionProblem,
                lad.PollutionType,
                lad.PollutionTypeOther,
                lad.HasOtherEnvironmentProblem,
                lad.OtherEnvironmentProblemRemark,
                lad.PollutionProblemRemark,
                lad.IsEncroached,
                lad.EncroachmentRemark,
                lad.IsLandlocked,
                lad.LandlockedRemark,
                lad.ExpropriationLineRemark,
                lad.RoyalDecree,
                lad.OtherLegalLimitations,
                lad.NorthAdjacentArea, lad.NorthBoundaryLength,
                lad.SouthAdjacentArea, lad.SouthBoundaryLength,
                lad.EastAdjacentArea,  lad.EastBoundaryLength,
                lad.WestAdjacentArea,  lad.WestBoundaryLength,
                lad.LandEntranceExitType,
                lad.PlotLocationType,
                lad.PublicUtilityType,
                lad.LandDescription,
                lad.LandShapeType,
                lad.DevelopmentSuitabilityType,
                lad.LandUseType,
                lad.TransportationAccessType,
                lad.TransportationAccessTypeOther,
                lad.PropertyAnticipationType,
                lad.IsInExpropriationLine,
                lad.UrbanPlanningType,
                lad.EnvironmentType,
                lad.EnvironmentTypeOther,
                lad.UrbanPlanningColor,
                lad.UrbanPlanningColorOther,
                lad.LandZoneType,
                lad.LandCheckMethodType,
                lad.LandCheckMethodTypeOther,
                lad.Remark,
                COALESCE(tsub.NameTh,  lad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, lad.District)    AS District,
                COALESCE(tprov.NameTh, lad.Province)    AS Province,
                lad.LandOffice,
                lad.EvictionType,
                lad.EvictionTypeOther,
                lad.EvictionRemark,
                lad.DeductedAreaInSqWa
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.PropertyGroups     pg  ON pg.Id = pgi.PropertyGroupId
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = lad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = lad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = lad.SubDistrict
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY COALESCE(pg.GroupNumber, 0), pgi.SequenceInGroup, ap.SequenceNumber, ap.Id;

            -- RS02: Q2 — All land titles, keyed by parent land detail for per-property grouping.
            SELECT
                lad.Id        AS LandDetailId,
                lt.TitleNumber,
                lt.TitleType,
                lt.LandParcelNumber,
                lt.SurveyNumber,
                lt.MapSheetNumber,
                lt.Rawang,
                lt.DocumentValidationResultType,
                lt.GovernmentPricePerSqWa,
                lt.GovernmentPrice,
                lt.BoundaryMarkerType,
                lt.BoundaryMarkerRemark,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa,
                lad.OwnerName
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, lt.SequenceNumber, lt.Id;

            -- RS03: Q3 — Thai descriptions for every coded land field, in one round-trip.
            -- Consumed as a group→(code→description) lookup; JsonCodesToThai falls back to
            -- the raw code for any value not present in the map.
            SELECT [group] AS [Group], [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN (
                  'RoadSurface', 'LandEntranceExit', 'PlotLocation', 'PublicUtility',
                  'LandUse', 'Transportation', 'Location', 'AnticipationOfProsperity',
                  'TypeOfUrbanPlanning', 'LandOffice', 'CheckBy', 'Landfill', 'LandAccessibility', 'BoundaryMarker', 'LandShape', 'DevelopmentSuitability', 'UrbanPlanningColor', 'Pollution', 'Obligation', 'Environment', 'Eviction', 'LandAreaDeductionReason');

            -- RS04: the area deductions of every land, in the order the appraiser listed them (SequenceNumber, then Id).
            SELECT d.LandAppraisalDetailId AS LandDetailId, d.ReasonCode, d.ReasonOther, d.AreaInSqWa, d.Remark
            FROM appraisal.LandAreaDeductions d
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = d.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY d.LandAppraisalDetailId, d.SequenceNumber, d.Id;
            """;

        List<LandDetailRow> landRows;
        List<TitleRow> titleRows;
        List<ParamRow> paramRows;
        List<DeductionRow> deductionRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01: Q1 — one row per land property
            landRows = (await multi.ReadAsync<LandDetailRow>()).ToList();

            // RS02: Q2 — title rows (always read to drain the result set)
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();

            // RS03: Q3 — parameter code→Thai maps (always read to drain the result set)
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS04: the area deductions
            deductionRows = (await multi.ReadAsync<DeductionRow>()).ToList();
        }

        if (landRows.Count == 0)
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

        // Resolves one (group, raw-code-or-JSON-array) pair to its Thai description(s).
        string? Resolve(string group, string? raw) =>
            JsonCodesToThai(raw, paramMaps.GetValueOrDefault(group) ?? EmptyMap);

        // Titles grouped by their parent land detail, so each property gets only its own.
        var titlesByDetail = titleRows
            .GroupBy(t => t.LandDetailId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Build one LandSection per land property ───────────────────────────────
        var sections = new List<LandSection>(landRows.Count);
        foreach (var landRow in landRows)
        {
            var rows = titlesByDetail.GetValueOrDefault(landRow.LandDetailId) ?? [];

            var titles = rows
                .Select((r, i) => new LandTitleRow
                {
                    Sequence         = i + 1,
                    // Each parcel names its own document; the column header used to assert โฉนด
                    // for every row regardless of what was selected (CA-609).
                    TitleTypeDisplay = TitleDeedLabel.Noun(r.TitleType),
                    TitleNumber      = r.TitleNumber,
                    LandParcelNumber = r.LandParcelNumber,
                    SurveyNumber     = r.SurveyNumber,
                    MapSheet         = TitleSheet(r.TitleType, r.Rawang, r.MapSheetNumber),
                    Rawang           = AppraisalSummaryCommonLoader.FirstNonBlank(r.Rawang),
                    DocumentValidationResult = DocumentCheck.Result(r.DocumentValidationResultType),
                    GovernmentPricePerSqWa = r.GovernmentPricePerSqWa,
                    GovernmentPrice  = r.GovernmentPrice,
                    AreaRai          = r.AreaRai,
                    AreaNgan         = r.AreaNgan,
                    AreaSquareWa     = r.AreaSquareWa,
                    OwnerName        = r.OwnerName
                })
                .ToList();

            // รวมเนื้อที่ดิน — normalised total across this property's titles
            var total = ThaiLandAreaFormatter.NormalizeTotal(
                rows.Sum(r => r.AreaRai ?? 0m),
                rows.Sum(r => r.AreaNgan ?? 0m),
                rows.Sum(r => r.AreaSquareWa ?? 0m));

            // ตรวจสอบจาก — a multi-select: the codes (a bare code on older rows, a JSON array now) → Thai, joined ", ".
            // Code 99 ("other") shows the free-text remark instead of the "อื่นๆ" label.
            string? checkedFrom = JsonCodesToThai(landRow.LandCheckMethodType, paramMaps.GetValueOrDefault("CheckBy") ?? EmptyMap, landRow.LandCheckMethodTypeOther);

            var evictionType = JsonCodesToThai(landRow.EvictionType, paramMaps.GetValueOrDefault("Eviction") ?? EmptyMap, landRow.EvictionTypeOther);
            var reasonMap = paramMaps.GetValueOrDefault("LandAreaDeductionReason") ?? EmptyMap;
            var deductions = deductionRows.Where(d => d.LandDetailId == landRow.LandDetailId)
                .Select((d, i) => new LandDeductionRow
                {
                    Sequence = i + 1,
                    // Code 99 ("other") shows its own text instead of the "สาเหตุอื่น" label.
                    Reason = d.ReasonCode == OtherCode && !string.IsNullOrWhiteSpace(d.ReasonOther)
                        ? d.ReasonOther.Trim()
                        : (reasonMap.TryGetValue(d.ReasonCode ?? "", out var label) && !string.IsNullOrWhiteSpace(label) ? label : d.ReasonCode),
                    AreaInSqWa = d.AreaInSqWa,
                    Remark = AppraisalSummaryCommonLoader.FirstNonBlank(d.Remark)
                }).ToList();

            sections.Add(new LandSection
            {
                EvictionType           = evictionType,
                EvictionRemark         = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.EvictionRemark),
                Deductions             = deductions,
                GroupNumber            = landRow.GroupNumber,
                GroupName              = landRow.GroupName,
                TotalRai               = total.Rai,
                TotalNgan              = total.Ngan,
                TotalSquareWa          = total.Wa,
                TotalAreaInWa          = total.TotalSquareWa,
                // Registered area stands; the deduction and what is left print under it, and only
                // when there is something to deduct — an unencroached property reads as before.
                DeductedAreaInWa       = landRow.DeductedAreaInSqWa ?? 0m,
                NetAreaInWa            = Math.Max(0m, total.TotalSquareWa - (landRow.DeductedAreaInSqWa ?? 0m)),
                CheckedFrom            = checkedFrom,
                MapSheets              = titles.Select(t => t.MapSheet).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList() is { Count: > 0 } sheets
                                             ? string.Join(", ", sheets) : null,
                SubDistrict            = landRow.SubDistrict,
                District               = landRow.District,
                Province               = landRow.Province,
                LandOffice             = Resolve("LandOffice", landRow.LandOffice),
                ObligationMortgage     = ObligationText.Mortgage(landRow.ObligationType, landRow.MortgageDetails),
                ObligationOthers       = ObligationText.Others(landRow.ObligationType, landRow.ObligationDetails, paramMaps.GetValueOrDefault(ObligationText.ParameterGroup) ?? EmptyMap),
                RoadPassInFrontOfLand  = landRow.RoadPassInFrontOfLand,
                RoadSurfaceType        = JsonCodesToThai(landRow.RoadSurfaceType, paramMaps.GetValueOrDefault("RoadSurface") ?? EmptyMap, landRow.RoadSurfaceTypeOther),
                CarriagewayWidth       = landRow.CarriagewayWidth,
                RightOfWayWidth             = landRow.RightOfWayWidth,
                RoadFrontage           = landRow.RoadFrontage,
                NumberOfLanes              = landRow.NumberOfLanes,
                NumberOfSidesFacingRoad = landRow.NumberOfSidesFacingRoad,
                IsLandLocationVerified = landRow.IsLandLocationVerified,
                LandLocationRemark = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.LandLocationRemark),
                IsOwnerVerified        = landRow.IsOwnerVerified,
                IsRentedOut            = landRow.IsRentedOut,
                Latitude               = landRow.Latitude,
                Longitude              = landRow.Longitude,
                DistanceFromMainRoad   = landRow.DistanceFromMainRoad,
                TransportationAccessType = JsonCodesToThai(landRow.TransportationAccessType, paramMaps.GetValueOrDefault("Transportation") ?? EmptyMap, landRow.TransportationAccessTypeOther),
                LandEntranceExitType   = Resolve("LandEntranceExit", landRow.LandEntranceExitType),
                PlotLocationType       = Resolve("PlotLocation", landRow.PlotLocationType),
                PublicUtilityType      = Resolve("PublicUtility", landRow.PublicUtilityType),
                // รูปแปลงที่ดิน → LandShapeType (code, group "LandShape"), not LandDescription
                LandDescription        = Resolve("LandShape", landRow.LandShapeType),
                // ความเหมาะสมต่อการพัฒนา → DevelopmentSuitabilityType (code, group "DevelopmentSuitability")
                DevelopmentSuitability = Resolve("DevelopmentSuitability", landRow.DevelopmentSuitabilityType),
                LandDescriptionText    = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.LandDescription),
                LandUseType            = Resolve("LandUse", landRow.LandUseType),
                PropertyAnticipationType = Resolve("AnticipationOfProsperity", landRow.PropertyAnticipationType),
                IsInExpropriationLine  = landRow.IsInExpropriationLine,
                UrbanPlanningType      = Resolve("TypeOfUrbanPlanning", landRow.UrbanPlanningType),
                EnvironmentType        = JsonCodesToThai(landRow.EnvironmentType, paramMaps.GetValueOrDefault("Environment") ?? EmptyMap, landRow.EnvironmentTypeOther),
                UrbanPlanningColor     = JsonCodesToThai(landRow.UrbanPlanningColor, paramMaps.GetValueOrDefault("UrbanPlanningColor") ?? EmptyMap, landRow.UrbanPlanningColorOther),
                LandZoneType           = Resolve("Location", landRow.LandZoneType),
                IsForestBoundary       = landRow.IsForestBoundary,
                ForestBoundaryRemark   = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.ForestBoundaryRemark),
                HasFloodProblem        = landRow.HasFloodProblem,
                FloodProblemRemark     = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.FloodProblemRemark),
                HasPollutionProblem    = landRow.HasPollutionProblem,
                PollutionType          = JsonCodesToThai(landRow.PollutionType, paramMaps.GetValueOrDefault("Pollution") ?? EmptyMap, landRow.PollutionTypeOther, " / "),
                PollutionProblemRemark = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.PollutionProblemRemark),
                HasOtherEnvironmentProblem = landRow.HasOtherEnvironmentProblem,
                OtherEnvironmentProblemRemark = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.OtherEnvironmentProblemRemark),
                IsEncroached           = landRow.IsEncroached,
                EncroachmentRemark     = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.EncroachmentRemark),
                IsLandlocked           = landRow.IsLandlocked,
                LandlockedRemark       = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.LandlockedRemark),
                ExpropriationRemark    = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.ExpropriationLineRemark),
                RoyalDecree            = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.RoyalDecree),
                OtherLegalLimitations  = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.OtherLegalLimitations),
                LandFill               = landRow.LandFillType == OtherCode && !string.IsNullOrWhiteSpace(landRow.LandFillTypeOther)
                                             ? landRow.LandFillTypeOther
                                             : Resolve("Landfill", landRow.LandFillType) ?? AppraisalSummaryCommonLoader.FirstNonBlank(landRow.LandFillTypeOther),
                LandFillPercent        = landRow.LandFillPercent,
                SoilLevel              = landRow.SoilLevel,
                PondArea               = landRow.PondArea,
                PondDepth              = landRow.PondDepth,
                LandAccessibility      = Resolve("LandAccessibility", landRow.LandAccessibilityType),
                LandAccessibilityRemark = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.LandAccessibilityRemark),
                FarFromElectricity     = landRow.FarFromElectricity,
                ElectricityDistance    = landRow.ElectricityDistance,
                BoundaryMarker         = BoundaryMarkerText(rows, Resolve),
                Boundaries             = BuildBoundaries(landRow),
                Remark                 = AppraisalSummaryCommonLoader.FirstNonBlank(landRow.Remark),
                Titles                 = titles
            });
        }

        return sections;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Parameter code that means "other" — its paired *Other free text is shown
    /// instead of the generic "อื่นๆ" description.</summary>
    private const string OtherCode = "99";

    /// <summary>Shared empty map so unmapped groups fall through to the raw code.</summary>
    private static readonly IReadOnlyDictionary<string, string?> EmptyMap =
        new Dictionary<string, string?>();

    /// <summary>
    /// Parses a JSON array (e.g. <c>["01","02"]</c>) — or a single code — and joins the
    /// matching Thai descriptions from <paramref name="map"/>, comma-separated. Any code
    /// not present in the map falls back to the raw code. Returns null for null/empty input.
    ///
    /// Mirrors <c>JsonCodesToThai</c> in <c>AppraisalSummaryLandBuildingDataProvider</c>.
    /// </summary>
    internal static string? JsonCodesToThai(string? raw, IReadOnlyDictionary<string, string?> map, string? other = null, string separator = ", ")
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        List<string> codes;
        if (s.StartsWith('['))
        {
            try
            {
                codes = JsonSerializer.Deserialize<List<string>>(s) ?? [];
            }
            catch (JsonException)
            {
                codes = [s];
            }
        }
        else
        {
            codes = [s];
        }

        // Code "99" = other: the remark replaces the "อื่นๆ" label. It prints only while 99 is ticked: text left in the box after
        // 99 was unticked is stale and must not appear (this is the one place every caller resolves through).
        var remark = string.IsNullOrWhiteSpace(other) ? null : other.Trim();
        var labels = AppraisalSummaryCommonLoader.OrderCodes(codes.Where(c => !string.IsNullOrWhiteSpace(c)))
            .Select(c => c == OtherCode && remark != null ? remark : map.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d! : c)
            .ToList();

        var joined = string.Join(separator, labels);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    /// <summary>
    /// The sheet a title prints: ระวาง (Rawang) belongs to a โฉนด, แผ่นที่ (MapSheetNumber) to an น.ส.3 ก. They are two names for one slot,
    /// so the one that matches the document type leads and the other only stands in when it is blank (a stale value left in the
    /// other field after the type was changed never wins). Any other type: ระวาง first.
    /// </summary>
    internal static string? TitleSheet(string? titleType, string? rawang, string? mapSheetNumber) =>
        string.Equals(titleType?.Trim(), "NS3K", StringComparison.OrdinalIgnoreCase)
            ? AppraisalSummaryCommonLoader.FirstNonBlank(mapSheetNumber, rawang)
            : AppraisalSummaryCommonLoader.FirstNonBlank(rawang, mapSheetNumber);

    /// <summary>
    /// หลักเขตที่พบ across a property's titles: each distinct "พบหลักเขต (remark)" once, joined with ", ".
    /// </summary>
    private static string? BoundaryMarkerText(List<TitleRow> titles, Func<string, string?, string?> resolve)
    {
        var parts = titles
            .Select(t => JoinNonEmpty(resolve("BoundaryMarker", t.BoundaryMarkerType), t.BoundaryMarkerRemark))
            .Where(x => x is not null)
            .Distinct()
            .ToList();
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    /// <summary>The four compass sides that carry something to print, in N/S/E/W order.</summary>
    private static List<LandBoundaryRow> BuildBoundaries(LandDetailRow r)
    {
        var all = new[]
        {
            new LandBoundaryRow { Direction = "ทิศเหนือ", Adjacent = r.NorthAdjacentArea, Distance = r.NorthBoundaryLength },
            new LandBoundaryRow { Direction = "ทิศใต้", Adjacent = r.SouthAdjacentArea, Distance = r.SouthBoundaryLength },
            new LandBoundaryRow { Direction = "ทิศตะวันออก", Adjacent = r.EastAdjacentArea, Distance = r.EastBoundaryLength },
            new LandBoundaryRow { Direction = "ทิศตะวันตก", Adjacent = r.WestAdjacentArea, Distance = r.WestBoundaryLength },
        };
        return all.Where(b => !string.IsNullOrWhiteSpace(b.Adjacent) || b.Distance.HasValue).ToList();
    }

    /// <summary>Space-joins the non-empty parts; returns null when all are empty.</summary>
    private static string? JoinNonEmpty(params string?[] parts)
    {
        var joined = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class DeductionRow
    {
        public Guid LandDetailId { get; init; }
        public string? ReasonCode { get; init; }
        public string? ReasonOther { get; init; }
        public decimal? AreaInSqWa { get; init; }
        public string? Remark { get; init; }
    }

    private sealed class LandDetailRow
    {
        public Guid LandDetailId { get; init; }
        public string? EvictionType { get; init; }
        public string? EvictionTypeOther { get; init; }
        public string? EvictionRemark { get; init; }
        public decimal? DeductedAreaInSqWa { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public string? OwnerName { get; init; }
        public string? ObligationDetails { get; init; }
        public string? ObligationType { get; init; }
        public string? MortgageDetails { get; init; }
        public string? RoadPassInFrontOfLand { get; init; }
        public string? RoadSurfaceType { get; init; }
        public string? RoadSurfaceTypeOther { get; init; }
        public decimal? CarriagewayWidth { get; init; }
        public decimal? RightOfWayWidth { get; init; }
        public decimal? RoadFrontage { get; init; }
        public short? NumberOfLanes { get; init; }
        public int? NumberOfSidesFacingRoad { get; init; }
        public bool? IsLandLocationVerified { get; init; }
        public string? LandLocationRemark { get; init; }
        public bool? IsOwnerVerified { get; init; }
        public bool? IsRentedOut { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public decimal? DistanceFromMainRoad { get; init; }
        public string? LandFillType { get; init; }
        public string? LandFillTypeOther { get; init; }
        public decimal? LandFillPercent { get; init; }
        public decimal? SoilLevel { get; init; }
        public decimal? PondArea { get; init; }
        public decimal? PondDepth { get; init; }
        public string? LandAccessibilityType { get; init; }
        public string? LandAccessibilityRemark { get; init; }
        public bool? FarFromElectricity { get; init; }
        public decimal? ElectricityDistance { get; init; }
        public bool? IsForestBoundary { get; init; }
        public string? ForestBoundaryRemark { get; init; }
        public bool? HasFloodProblem { get; init; }
        public string? FloodProblemRemark { get; init; }
        public bool? HasPollutionProblem { get; init; }
        public string? PollutionType { get; init; }
        public string? PollutionTypeOther { get; init; }
        public string? PollutionProblemRemark { get; init; }
        public bool? HasOtherEnvironmentProblem { get; init; }
        public string? OtherEnvironmentProblemRemark { get; init; }
        public bool? IsEncroached { get; init; }
        public string? EncroachmentRemark { get; init; }
        public bool? IsLandlocked { get; init; }
        public string? LandlockedRemark { get; init; }
        public string? ExpropriationLineRemark { get; init; }
        public string? RoyalDecree { get; init; }
        public string? OtherLegalLimitations { get; init; }
        public string? NorthAdjacentArea { get; init; }
        public decimal? NorthBoundaryLength { get; init; }
        public string? SouthAdjacentArea { get; init; }
        public decimal? SouthBoundaryLength { get; init; }
        public string? EastAdjacentArea { get; init; }
        public decimal? EastBoundaryLength { get; init; }
        public string? WestAdjacentArea { get; init; }
        public decimal? WestBoundaryLength { get; init; }

        // JSON array columns — decoded by JsonArrayToDisplay
        public string? LandEntranceExitType { get; init; }
        public string? PlotLocationType { get; init; }
        public string? PublicUtilityType { get; init; }
        public string? LandUseType { get; init; }
        public string? TransportationAccessType { get; init; }
        public string? TransportationAccessTypeOther { get; init; }
        public string? LandZoneType { get; init; }

        public string? LandDescription { get; init; }
        public string? LandShapeType { get; init; }
        public string? DevelopmentSuitabilityType { get; init; }
        public string? PropertyAnticipationType { get; init; }
        public bool? IsInExpropriationLine { get; init; }
        public string? UrbanPlanningType { get; init; }
        public string? EnvironmentType { get; init; }
        public string? EnvironmentTypeOther { get; init; }
        public string? UrbanPlanningColor { get; init; }
        public string? UrbanPlanningColorOther { get; init; }
        public string? LandCheckMethodType { get; init; }
        public string? LandCheckMethodTypeOther { get; init; }
        public string? Remark { get; init; }

        // Address VO columns (HasColumnName in EF config) — geocodes already resolved
        // to Thai names (or raw fallback) in SQL via the parameter.Title* joins.
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
    }

    private sealed class TitleRow
    {
        public Guid LandDetailId { get; init; }
        public string? TitleNumber { get; init; }
        public string? TitleType { get; init; }
        public string? LandParcelNumber { get; init; }
        public string? SurveyNumber { get; init; }
        public string? MapSheetNumber { get; init; }
        public string? Rawang { get; init; }
        public string? DocumentValidationResultType { get; init; }
        public decimal? GovernmentPricePerSqWa { get; init; }
        public decimal? GovernmentPrice { get; init; }
        public string? BoundaryMarkerType { get; init; }
        public string? BoundaryMarkerRemark { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
        public string? OwnerName { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group { get; init; }
        public string? Code { get; init; }
        public string? Description { get; init; }
    }
}
