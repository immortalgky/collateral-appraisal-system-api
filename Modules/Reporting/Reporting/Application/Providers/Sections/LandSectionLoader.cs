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
        //   AccessRoadWidth / RoadFrontage (decimal 10,2),
        //   LandEntranceExitType / PlotLocationType / PublicUtilityType /
        //     LandUseType / TransportationAccessType / LandZoneType (nvarchar 500, JSON arrays),
        //   LandDescription / PropertyAnticipationType / UrbanPlanningType (nvarchar),
        //   IsExpropriated (bool) → IsInExpropriationLine (closest equivalent),
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
                lad.RoadPassInFrontOfLand,
                lad.RoadSurfaceType,
                lad.AccessRoadWidth,
                lad.RoadFrontage,
                lad.LandEntranceExitType,
                lad.PlotLocationType,
                lad.PublicUtilityType,
                lad.LandDescription,
                lad.LandShapeType,
                lad.LandUseType,
                lad.TransportationAccessType,
                lad.PropertyAnticipationType,
                lad.IsExpropriated            AS IsInExpropriationLine,
                lad.UrbanPlanningType,
                lad.LandZoneType,
                lad.LandCheckMethodType,
                lad.LandCheckMethodTypeOther,
                lad.Remark,
                COALESCE(tsub.NameTh,  lad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, lad.District)    AS District,
                COALESCE(tprov.NameTh, lad.Province)    AS Province,
                lad.LandOffice
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
                lt.LandParcelNumber,
                lt.SurveyNumber,
                lt.MapSheetNumber,
                lt.Rawang,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa,
                lad.OwnerName
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, lt.Id;

            -- RS03: Q3 — Thai descriptions for every coded land field, in one round-trip.
            -- Consumed as a group→(code→description) lookup; JsonCodesToThai falls back to
            -- the raw code for any value not present in the map.
            SELECT [group] AS [Group], [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN (
                  'RoadSurface', 'LandEntranceExit', 'PlotLocation', 'PublicUtility',
                  'LandUse', 'Transportation', 'Location', 'AnticipationOfProsperity',
                  'TypeOfUrbanPlanning', 'LandOffice', 'CheckBy');
            """;

        List<LandDetailRow> landRows;
        List<TitleRow> titleRows;
        List<ParamRow> paramRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01: Q1 — one row per land property
            landRows = (await multi.ReadAsync<LandDetailRow>()).ToList();

            // RS02: Q2 — title rows (always read to drain the result set)
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();

            // RS03: Q3 — parameter code→Thai maps (always read to drain the result set)
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();
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
                    TitleNumber      = r.TitleNumber,
                    LandParcelNumber = r.LandParcelNumber,
                    SurveyNumber     = r.SurveyNumber,
                    MapSheet         = JoinNonEmpty(r.MapSheetNumber, r.Rawang),
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

            // ตรวจสอบจาก — check-method code → Thai. Code 99 ("other") shows the free-text
            // remark instead of the "อื่นๆ" label; for any other code we still fall back to
            // the remark only when the code resolves to nothing.
            string? checkedFrom = Resolve("CheckBy", landRow.LandCheckMethodType);
            if (landRow.LandCheckMethodType == OtherCode &&
                !string.IsNullOrWhiteSpace(landRow.LandCheckMethodTypeOther))
                checkedFrom = landRow.LandCheckMethodTypeOther;
            else if (string.IsNullOrWhiteSpace(checkedFrom))
                checkedFrom = landRow.LandCheckMethodTypeOther;

            sections.Add(new LandSection
            {
                GroupNumber            = landRow.GroupNumber,
                GroupName              = landRow.GroupName,
                TotalRai               = total.Rai,
                TotalNgan              = total.Ngan,
                TotalSquareWa          = total.Wa,
                TotalAreaInWa          = total.TotalSquareWa,
                CheckedFrom            = checkedFrom,
                SubDistrict            = landRow.SubDistrict,
                District               = landRow.District,
                Province               = landRow.Province,
                LandOffice             = Resolve("LandOffice", landRow.LandOffice),
                Obligation             = landRow.ObligationDetails,
                RoadPassInFrontOfLand  = landRow.RoadPassInFrontOfLand,
                RoadSurfaceType        = Resolve("RoadSurface", landRow.RoadSurfaceType),
                AccessRoadWidth        = landRow.AccessRoadWidth,
                RoadFrontage           = landRow.RoadFrontage,
                LandEntranceExitType   = Resolve("LandEntranceExit", landRow.LandEntranceExitType),
                PlotLocationType       = Resolve("PlotLocation", landRow.PlotLocationType),
                PublicUtilityType      = Resolve("PublicUtility", landRow.PublicUtilityType),
                // ลักษณะทางกายภาพ → LandShapeType (code, group "LandShape"), not LandDescription
                LandDescription        = Resolve("LandShape", landRow.LandShapeType),
                LandUseType            = Resolve("LandUse", landRow.LandUseType),
                // ทรัพย์สินอยู่ในพื้นที่ → UrbanPlanningType (group "TypeOfUrbanPlanning"), not TransportationAccessType
                TransportationAccessType = Resolve("TypeOfUrbanPlanning", landRow.UrbanPlanningType),
                PropertyAnticipationType = Resolve("AnticipationOfProsperity", landRow.PropertyAnticipationType),
                IsInExpropriationLine  = landRow.IsInExpropriationLine,
                UrbanPlanningType      = Resolve("TypeOfUrbanPlanning", landRow.UrbanPlanningType),
                LandZoneType           = Resolve("Location", landRow.LandZoneType),
                Remark                 = landRow.Remark,
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
    private static string? JsonCodesToThai(string? raw, IReadOnlyDictionary<string, string?> map)
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

        var labels = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => map.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d! : c);

        var joined = string.Join(", ", labels);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    /// <summary>Space-joins the non-empty parts; returns null when all are empty.</summary>
    private static string? JoinNonEmpty(params string?[] parts)
    {
        var joined = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class LandDetailRow
    {
        public Guid LandDetailId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public string? OwnerName { get; init; }
        public string? ObligationDetails { get; init; }
        public string? RoadPassInFrontOfLand { get; init; }
        public string? RoadSurfaceType { get; init; }
        public decimal? AccessRoadWidth { get; init; }
        public decimal? RoadFrontage { get; init; }

        // JSON array columns — decoded by JsonArrayToDisplay
        public string? LandEntranceExitType { get; init; }
        public string? PlotLocationType { get; init; }
        public string? PublicUtilityType { get; init; }
        public string? LandUseType { get; init; }
        public string? TransportationAccessType { get; init; }
        public string? LandZoneType { get; init; }

        public string? LandDescription { get; init; }
        public string? LandShapeType { get; init; }
        public string? PropertyAnticipationType { get; init; }
        public bool? IsInExpropriationLine { get; init; }
        public string? UrbanPlanningType { get; init; }
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
        public string? LandParcelNumber { get; init; }
        public string? SurveyNumber { get; init; }
        public string? MapSheetNumber { get; init; }
        public string? Rawang { get; init; }
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
