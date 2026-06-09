using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "ข้อมูลตลาดเปรียบเทียบ" (Comparison Information) section — FSD §2.1.2.8.
///
/// Navigation used (schema: appraisal.*):
///   PricingAnalysis (SubjectType=0, AnchorId = PropertyGroup of @appraisalId)
///     → PricingAnalysisApproaches (PricingAnalysisId)
///       → PricingAnalysisMethods (ApproachId)
///         → PricingComparableLinks (PricingMethodId, MarketComparableId, DisplaySequence)
///           → MarketComparables mc (Id, ComparableNumber, SurveyName, OfferPrice,
///                                    InfoDateTime, IsDeleted = 0)
///               → MarketComparableData mcd (MarketComparableId, FactorId, Value)
///                 → MarketComparableFactors mcf (Id, IsActive = 1)
///                   → MarketComparableFactorTranslations (Language = 'th', FactorName)
///
/// Two SQL queries are issued:
///   Q1: distinct linked comparables (ordered by MIN DisplaySequence for stable column order).
///   Q2: all EAV factor values for those comparables, plus factor labels (Thai translation,
///       fallback to English).
///
/// Fixed trailing rows (always appended after the EAV factor rows):
///   ราคาเสนอขาย  — mc.OfferPrice
///   วันที่ลงข้อมูล — mc.InfoDateTime (date portion only)
///   แหล่งที่มา     — mc.SurveyName
///
/// Returns <see langword="null"/> when no comparables are linked to this appraisal.
/// </summary>
internal static class ComparisonSectionLoader
{
    /// <summary>
    /// Loads the <see cref="ComparisonSection"/> for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when no pricing-method comparable links exist.
    /// </summary>
    /// <param name="connection">An open <see cref="IDbConnection"/> (Dapper, read-only).</param>
    /// <param name="appraisalId">The appraisal to load comparables for.</param>
    /// <param name="ct">Cancellation token (not forwarded to Dapper; cancellation is
    ///   cooperative at query boundaries).</param>
    public static async Task<ComparisonSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Distinct comparables linked to this appraisal's pricing methods ──
        //
        // The same MarketComparable can appear in multiple PricingMethods (e.g. WQS
        // and a second market approach). DISTINCT + MIN(DisplaySequence) de-dupes while
        // preserving a stable column order that matches the analyst's first-linked
        // sequence assignment.
        //
        // Columns sourced (all from appraisal.MarketComparables, IsDeleted = 0):
        //   mc.Id              — Guid key, used to join EAV data
        //   mc.ComparableNumber — reference code (e.g. "WQS-001")
        //   mc.SurveyName       — แหล่งที่มา (source/survey name)
        //   mc.OfferPrice       — ราคาเสนอขาย (decimal 18,2)
        //   mc.InfoDateTime     — วันที่ลงข้อมูล (datetime)
        const string comparablesSql = """
            SELECT DISTINCT
                mc.Id,
                mc.ComparableNumber,
                mc.SurveyName,
                mc.OfferPrice,
                mc.InfoDateTime,
                MIN(pcl.DisplaySequence) OVER (PARTITION BY mc.Id) AS MinDisplaySequence
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam    ON pam.ApproachId = paa.Id
            JOIN appraisal.PricingComparableLinks pcl    ON pcl.PricingMethodId = pam.Id
            JOIN appraisal.MarketComparables mc           ON mc.Id = pcl.MarketComparableId
                                                         AND mc.IsDeleted = 0
            JOIN appraisal.PropertyGroups pg              ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0
            -- ComparableNumber tiebreaker keeps the "ข้อมูล {n}" column order stable across renders
            -- when two comparables share a MinDisplaySequence.
            ORDER BY MinDisplaySequence, mc.ComparableNumber
            """;

        var comparableRows = (await connection.QueryAsync<ComparableRow>(comparablesSql, p)).ToList();

        if (comparableRows.Count == 0)
            return null;

        // ── Build column headers ──────────────────────────────────────────────────
        // Label each column "ข้อมูล {n}" (1-based) matching the FSD image.
        var comparableIds = comparableRows.Select(r => r.Id).ToList();
        var columnIndex = comparableRows
            .Select((r, i) => (r.Id, Index: i))
            .ToDictionary(x => x.Id, x => x.Index);

        var headers = comparableRows
            .Select((_, i) => $"ข้อมูล {i + 1}")
            .ToList();

        // ── Q2: EAV factor values for all linked comparables ─────────────────────
        //
        // Factor master table: appraisal.MarketComparableFactors
        // Label column: appraisal.MarketComparableFactorTranslations.FactorName
        //   Language priority: 'th' first, fall back to 'en'.
        //
        // Columns sourced:
        //   mcf.Id                 — Guid key for grouping (factor identity)
        //   COALESCE(th, en) label — Thai display label (MarketComparableFactorTranslations)
        //   mcf.FieldName          — internal field name (used as tiebreaker label fallback)
        //   mcd.MarketComparableId — to align value into the correct column
        //   mcd.Value              — NVARCHAR(MAX) EAV value
        //
        // IsActive = 1 guard on the factor master prevents defunct factors from
        // adding phantom rows to the report.
        //
        // DisplaySequence from PricingComparativeFactors is NOT used here because the
        // Reporting module must not depend on a specific PricingMethod; instead we order
        // by the factor's own Id for a deterministic, template-consistent row order.
        var factorParams = new DynamicParameters();
        factorParams.Add("ComparableIds", comparableIds);

        const string factorsSql = """
            SELECT
                mcf.Id                      AS FactorId,
                COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorLabel,
                mcd.MarketComparableId,
                mcd.Value
            FROM appraisal.MarketComparableData mcd
            JOIN appraisal.MarketComparableFactors mcf
                ON mcf.Id = mcd.FactorId
               AND mcf.IsActive = 1
            LEFT JOIN appraisal.MarketComparableFactorTranslations th
                ON th.MarketComparableFactorId = mcf.Id
               AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en
                ON en.MarketComparableFactorId = mcf.Id
               AND en.Language = 'en'
            WHERE mcd.MarketComparableId IN @ComparableIds
            ORDER BY mcf.Id, mcd.MarketComparableId
            """;

        var factorDataRows = (await connection.QueryAsync<FactorDataRow>(factorsSql, factorParams)).ToList();

        // ── Pivot EAV rows into factor-keyed dictionary ───────────────────────────
        // factorMap: FactorId → (label, values[colCount])
        var factorOrder = new List<Guid>();
        var factorLabels = new Dictionary<Guid, string?>();
        var factorValues = new Dictionary<Guid, string?[]>();

        foreach (var row in factorDataRows)
        {
            if (!factorValues.ContainsKey(row.FactorId))
            {
                factorOrder.Add(row.FactorId);
                factorLabels[row.FactorId] = row.FactorLabel;
                factorValues[row.FactorId] = new string?[comparableRows.Count];
            }

            if (columnIndex.TryGetValue(row.MarketComparableId, out var colIdx))
                factorValues[row.FactorId][colIdx] = row.Value;
        }

        // ── Build data rows ───────────────────────────────────────────────────────
        var rows = new List<ComparisonFactorRow>(factorOrder.Count + 3);

        foreach (var factorId in factorOrder)
        {
            rows.Add(new ComparisonFactorRow
            {
                FactorName = factorLabels[factorId],
                Values     = factorValues[factorId]
            });
        }

        // ── Fixed trailing rows ───────────────────────────────────────────────────
        // ราคาเสนอขาย — mc.OfferPrice (formatted as integer baht, no decimal)
        rows.Add(BuildFixedRow(
            "ราคาเสนอขาย",
            comparableRows,
            r => r.OfferPrice.HasValue
                ? r.OfferPrice.Value.ToString("N0")
                : null));

        // วันที่ลงข้อมูล — mc.InfoDateTime (date only: d/M/yyyy Thai-style)
        rows.Add(BuildFixedRow(
            "วันที่ลงข้อมูล",
            comparableRows,
            r => r.InfoDateTime.HasValue
                ? r.InfoDateTime.Value.ToString("d/M/yyyy")
                : null));

        // แหล่งที่มา — mc.SurveyName
        rows.Add(BuildFixedRow(
            "แหล่งที่มา",
            comparableRows,
            r => r.SurveyName));

        return new ComparisonSection
        {
            ComparableHeaders = headers,
            Rows              = rows
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static ComparisonFactorRow BuildFixedRow(
        string label,
        IReadOnlyList<ComparableRow> comparables,
        Func<ComparableRow, string?> valueSelector) =>
        new()
        {
            FactorName = label,
            Values     = comparables.Select(valueSelector).ToArray()
        };

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────────

    private sealed class ComparableRow
    {
        public Guid       Id                 { get; init; }
        public string?    ComparableNumber   { get; init; }
        public string?    SurveyName         { get; init; }
        public decimal?   OfferPrice         { get; init; }
        public DateTime?  InfoDateTime       { get; init; }
        public int        MinDisplaySequence { get; init; }
    }

    private sealed class FactorDataRow
    {
        public Guid    FactorId            { get; init; }
        public string? FactorLabel         { get; init; }
        public Guid    MarketComparableId  { get; init; }
        public string? Value               { get; init; }
    }
}
