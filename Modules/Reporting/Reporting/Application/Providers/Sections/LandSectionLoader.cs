using System.Data;
using System.Text.Json;
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
    /// Loads the <see cref="LandSection"/> for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when no land properties exist for the appraisal.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load land details for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<LandSection?> LoadAsync(
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
            -- RS01: Q1 — Land detail header (first property by SequenceNumber)
            SELECT TOP 1
                lad.Id                       AS LandDetailId,
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
                lad.LandUseType,
                lad.TransportationAccessType,
                lad.PropertyAnticipationType,
                lad.IsExpropriated            AS IsInExpropriationLine,
                lad.UrbanPlanningType,
                lad.LandZoneType,
                lad.Remark,
                lad.SubDistrict,
                lad.District,
                lad.Province,
                lad.LandOffice
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, ap.Id;

            -- RS02: Q2 — All land titles across all land properties
            -- Ordered by (SequenceNumber, LandTitles.Id) for stable printed sequence.
            SELECT
                lt.TitleNumber,
                lt.LandParcelNumber,
                lt.SurveyNumber,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa,
                lad.OwnerName
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, lt.Id;
            """;

        LandDetailRow? landRow;
        List<TitleRow> titleRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01: Q1 — land header
            landRow = await multi.ReadFirstOrDefaultAsync<LandDetailRow>();

            // RS02: Q2 — title rows (always read to drain the result set)
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();
        }

        if (landRow is null)
            return null;

        // ── Build title rows ──────────────────────────────────────────────────────
        var titles = titleRows
            .Select((r, i) => new LandTitleRow
            {
                Sequence       = i + 1,
                TitleNumber    = r.TitleNumber,
                LandParcelNumber = r.LandParcelNumber,
                SurveyNumber   = r.SurveyNumber,
                AreaRai        = r.AreaRai,
                AreaNgan       = r.AreaNgan,
                AreaSquareWa   = r.AreaSquareWa,
                OwnerName      = r.OwnerName
            })
            .ToList();

        // ── Compute total area across all titles ──────────────────────────────────
        string? totalAreaText = BuildTotalAreaText(titleRows);

        // ── Build section ─────────────────────────────────────────────────────────
        return new LandSection
        {
            TotalAreaText          = totalAreaText,
            SubDistrict            = landRow.SubDistrict,
            District               = landRow.District,
            Province               = landRow.Province,
            LandOffice             = landRow.LandOffice,
            Obligation             = landRow.ObligationDetails,
            RoadPassInFrontOfLand  = landRow.RoadPassInFrontOfLand,
            RoadSurfaceType        = landRow.RoadSurfaceType,
            AccessRoadWidth        = landRow.AccessRoadWidth,
            RoadFrontage           = landRow.RoadFrontage,
            LandEntranceExitType   = JsonArrayToDisplay(landRow.LandEntranceExitType),
            PlotLocationType       = JsonArrayToDisplay(landRow.PlotLocationType),
            PublicUtilityType      = JsonArrayToDisplay(landRow.PublicUtilityType),
            LandDescription        = landRow.LandDescription,
            LandUseType            = JsonArrayToDisplay(landRow.LandUseType),
            TransportationAccessType = JsonArrayToDisplay(landRow.TransportationAccessType),
            PropertyAnticipationType = landRow.PropertyAnticipationType,
            IsInExpropriationLine  = landRow.IsInExpropriationLine,
            UrbanPlanningType      = landRow.UrbanPlanningType,
            LandZoneType           = JsonArrayToDisplay(landRow.LandZoneType),
            Remark                 = landRow.Remark,
            Titles                 = titles
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sums all title areas and formats as
    /// "{rai} - {ngan} - {wa} ไร่ หรือ {totalSqWa} ตารางวา".
    /// Returns null when no titles exist or all area values are null/zero.
    /// </summary>
    private static string? BuildTotalAreaText(IReadOnlyList<TitleRow> rows)
    {
        if (rows.Count == 0)
            return null;

        decimal totalRai  = rows.Sum(r => r.AreaRai  ?? 0m);
        decimal totalNgan = rows.Sum(r => r.AreaNgan  ?? 0m);
        decimal totalSqWa = rows.Sum(r => r.AreaSquareWa ?? 0m);

        // Convert to total square-wa: 1 rai = 400 sqwa, 1 ngan = 100 sqwa
        decimal totalSqWaAbsolute = totalRai * 400m + totalNgan * 100m + totalSqWa;

        if (totalRai == 0m && totalNgan == 0m && totalSqWa == 0m)
            return null;

        return $"{totalRai:0.##} - {totalNgan:0.##} - {totalSqWa:0.##} ไร่ หรือ {totalSqWaAbsolute:0.##} ตารางวา";
    }

    /// <summary>
    /// Decodes a JSON-array string (e.g. <c>["Residential","Commercial"]</c>) to a
    /// comma-joined display string. Falls back to the raw value when it is not valid
    /// JSON. Returns null for null/empty input.
    ///
    /// Mirrors the same helper in <c>AppraisalSummaryLandBuildingDataProvider</c>.
    /// </summary>
    private static string? JsonArrayToDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (!s.StartsWith('['))
            return s;

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(s);
            if (items is null || items.Count == 0)
                return null;
            var joined = string.Join(", ", items.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
        catch (JsonException)
        {
            return s;
        }
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class LandDetailRow
    {
        public Guid LandDetailId { get; init; }
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
        public string? PropertyAnticipationType { get; init; }
        public bool? IsInExpropriationLine { get; init; }
        public string? UrbanPlanningType { get; init; }
        public string? Remark { get; init; }

        // Address VO columns (HasColumnName in EF config)
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
    }

    private sealed class TitleRow
    {
        public string? TitleNumber { get; init; }
        public string? LandParcelNumber { get; init; }
        public string? SurveyNumber { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
        public string? OwnerName { get; init; }
    }
}
