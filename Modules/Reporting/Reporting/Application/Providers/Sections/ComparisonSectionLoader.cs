using System.Data;
using System.Text.Json;
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
///           → MarketComparables mc (Id, ComparableNumber, PropertyType, SurveyName,
///                                    OfferPrice, InfoDateTime, IsDeleted = 0)
///               → MarketComparableData mcd (MarketComparableId, FactorId, Value)
///                 → MarketComparableFactors mcf (Id, DataType, ParameterGroup, IsActive = 1)
///                   → MarketComparableFactorTranslations (Language = 'th', FactorName)
///
/// Three SQL queries are issued:
///   Q1: distinct linked comparables (ordered by MIN DisplaySequence for stable column order).
///   Q2: all EAV factor values for those comparables, plus factor labels (Thai translation,
///       fallback to English) and the factor's DataType / ParameterGroup.
///   Q3: Thai parameter descriptions for every parameter group in use, plus PropertyType,
///       so coded factor values (Dropdown/Radio/Checkbox/CheckboxGroup) render as descriptions.
///
/// Comparables are split into one <see cref="ComparisonTable"/> per
/// <c>MarketComparable.PropertyType</c>, ordered by first appearance (lowest DisplaySequence).
///
/// Fixed trailing rows (always appended after the EAV factor rows of each table):
///   ราคาเสนอขาย  — mc.OfferPrice
///   วันที่ลงข้อมูล — mc.InfoDateTime (date portion only)
///   แหล่งที่มา     — mc.SurveyName
///
/// Returns <see langword="null"/> when no comparables are linked to this appraisal.
/// </summary>
internal static class ComparisonSectionLoader
{
    /// <summary>Parameter group used to resolve <c>PropertyType</c> codes to Thai labels.</summary>
    private const string PropertyTypeGroup = "PropertyType";

    /// <summary>
    /// Loads all <see cref="ComparisonSection"/>s for the given <paramref name="appraisalId"/>.
    /// Returns a list of one section (all comparables share a single section regardless of
    /// property group). Returns an empty list when no pricing-method comparable links exist.
    /// </summary>
    /// <param name="connection">An open <see cref="IDbConnection"/> (Dapper, read-only).</param>
    /// <param name="appraisalId">The appraisal to load comparables for.</param>
    /// <param name="ct">Cancellation token (not forwarded to Dapper; cancellation is
    ///   cooperative at query boundaries).</param>
    public static async Task<IReadOnlyList<ComparisonSection>> LoadAllAsync(
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
        //   mc.PropertyType     — grouping key (L / LB / U / B / …)
        //   mc.SurveyName       — แหล่งที่มา (source/survey name)
        //   mc.OfferPrice       — ราคาเสนอขาย (decimal 18,2)
        //   mc.InfoDateTime     — วันที่ลงข้อมูล (datetime)
        const string comparablesSql = """
            SELECT DISTINCT
                mc.Id,
                mc.ComparableNumber,
                mc.PropertyType,
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
            return [];

        var comparableIds = comparableRows.Select(r => r.Id).ToList();

        // ── Q2: EAV factor values for all linked comparables ─────────────────────
        //
        // Factor master table: appraisal.MarketComparableFactors
        // Label column: appraisal.MarketComparableFactorTranslations.FactorName
        //   Language priority: 'th' first, fall back to 'en'.
        //
        // Columns sourced:
        //   mcf.Id                 — Guid key for grouping (factor identity)
        //   COALESCE(th, en) label — Thai display label (MarketComparableFactorTranslations)
        //   mcf.DataType           — drives code→description resolution (Dropdown/Radio/Checkbox…)
        //   mcf.ParameterGroup     — parameter.Parameters group key for resolution
        //   mcd.MarketComparableId — to align value into the correct column
        //   mcd.Value              — NVARCHAR(MAX) EAV value (code, JSON-array of codes, or free text)
        //
        // IsActive = 1 guard on the factor master prevents defunct factors from
        // adding phantom rows to the report.
        //
        // Row order is by the factor's own Id for a deterministic, template-consistent order
        // (the Reporting module must not depend on a specific PricingMethod's DisplaySequence).
        var factorParams = new DynamicParameters();
        factorParams.Add("ComparableIds", comparableIds);

        const string factorsSql = """
            SELECT
                mcf.Id                      AS FactorId,
                COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS FactorLabel,
                mcf.DataType,
                mcf.ParameterGroup,
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

        // ── Q3: Thai parameter descriptions for code→description resolution ───────
        // Scope to the parameter groups actually referenced by the factors in play,
        // plus PropertyType (for the per-table heading). One round-trip; falls back to
        // the raw code for any code not present in the map.
        var paramGroups = factorDataRows
            .Where(r => !string.IsNullOrWhiteSpace(r.ParameterGroup))
            .Select(r => r.ParameterGroup!)
            .Append(PropertyTypeGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paramMaps = await LoadParameterMapsAsync(connection, paramGroups);

        // ── Group comparables by PropertyType, preserving first-appearance order ──
        // comparableRows is already ordered by MinDisplaySequence; GroupBy preserves the
        // order in which each key is first seen, so groups come out in first-appearance order.
        var groups = comparableRows
            .GroupBy(r => r.PropertyType ?? string.Empty)
            .ToList();

        var tables = new List<ComparisonTable>(groups.Count);
        foreach (var group in groups)
        {
            tables.Add(BuildTable(group.ToList(), factorDataRows, paramMaps));
        }

        // Wrap in a list of 1. The comparison section aggregates all property groups'
        // comparables into a single section — GroupNumber = 0, GroupName = null.
        return [new ComparisonSection { Tables = tables }];
    }

    // ── Table builder ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds one <see cref="ComparisonTable"/> for a single property-type group:
    /// renumbered "ข้อมูล {n}" headers, pivoted factor rows (only factors present in this
    /// group's comparables, coded values resolved to Thai), and the fixed trailing rows.
    /// </summary>
    private static ComparisonTable BuildTable(
        IReadOnlyList<ComparableRow> comparables,
        IReadOnlyList<FactorDataRow> factorDataRows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps)
    {
        var memberIds = comparables.Select(c => c.Id).ToHashSet();

        // Column index local to this table.
        var columnIndex = comparables
            .Select((c, i) => (c.Id, Index: i))
            .ToDictionary(x => x.Id, x => x.Index);

        var headers = comparables
            .Select((_, i) => $"ข้อมูล {i + 1}")
            .ToList();

        // Pivot EAV rows (for this group's comparables only) into a factor-keyed dictionary.
        var factorOrder = new List<Guid>();
        var factorLabels = new Dictionary<Guid, string?>();
        var factorValues = new Dictionary<Guid, string?[]>();

        foreach (var row in factorDataRows)
        {
            if (!memberIds.Contains(row.MarketComparableId))
                continue;

            if (!factorValues.ContainsKey(row.FactorId))
            {
                factorOrder.Add(row.FactorId);
                factorLabels[row.FactorId] = row.FactorLabel;
                factorValues[row.FactorId] = new string?[comparables.Count];
            }

            if (columnIndex.TryGetValue(row.MarketComparableId, out var colIdx))
                factorValues[row.FactorId][colIdx] = ResolveValue(row, paramMaps);
        }

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
            comparables,
            r => r.OfferPrice.HasValue
                ? r.OfferPrice.Value.ToString("N0")
                : null));

        // วันที่ลงข้อมูล — mc.InfoDateTime (date only: d/M/yyyy Thai-style)
        rows.Add(BuildFixedRow(
            "วันที่ลงข้อมูล",
            comparables,
            r => r.InfoDateTime.HasValue
                ? r.InfoDateTime.Value.ToString("d/M/yyyy")
                : null));

        // แหล่งที่มา — mc.SurveyName
        rows.Add(BuildFixedRow(
            "แหล่งที่มา",
            comparables,
            r => r.SurveyName));

        // Heading — Thai description of the property type (codes shared across the group).
        var propertyTypeCode = comparables[0].PropertyType;
        var propertyTypeLabel = ResolveCode(propertyTypeCode, paramMaps.GetValueOrDefault(PropertyTypeGroup));

        return new ComparisonTable
        {
            PropertyTypeLabel = propertyTypeLabel,
            ComparableHeaders = headers,
            Rows              = rows
        };
    }

    // ── Value resolution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a factor cell value: parameter-backed DataTypes (Dropdown/Radio/Checkbox/
    /// CheckboxGroup) with a ParameterGroup are mapped code→Thai (JSON-array aware); all other
    /// DataTypes (Text/Numeric/Date) pass through unchanged.
    /// </summary>
    private static string? ResolveValue(
        FactorDataRow row,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps)
    {
        if (!IsParameterBacked(row.DataType) || string.IsNullOrWhiteSpace(row.ParameterGroup))
            return row.Value;

        return JsonCodesToThai(row.Value, paramMaps.GetValueOrDefault(row.ParameterGroup) ?? EmptyMap);
    }

    /// <summary>
    /// True for factor DataTypes whose stored value is a parameter code (or JSON array of
    /// codes) rather than free text. DataType is persisted as the enum's string name.
    /// </summary>
    private static bool IsParameterBacked(string? dataType) => dataType is
        "Dropdown" or "Radio" or "Checkbox" or "CheckboxGroup";

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

    /// <summary>Shared empty map so unmapped groups fall through to the raw code.</summary>
    private static readonly IReadOnlyDictionary<string, string?> EmptyMap =
        new Dictionary<string, string?>();

    /// <summary>
    /// Loads <c>parameter.Parameters</c> Thai descriptions for the given groups and shapes
    /// them into a <c>group → (code → description)</c> lookup. Mirrors LandSectionLoader.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>>
        LoadParameterMapsAsync(IDbConnection connection, IReadOnlyList<string> groups)
    {
        if (groups.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<string, string?>>();

        var p = new DynamicParameters();
        p.Add("Groups", groups);

        const string sql = """
            SELECT [group] AS [Group], [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN @Groups
            """;

        var paramRows = (await connection.QueryAsync<ParamRow>(sql, p)).ToList();

        return paramRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Group) && !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.Group!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, string?>)g
                    .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Description, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves a single code to its Thai description, falling back to the raw code.</summary>
    private static string? ResolveCode(string? code, IReadOnlyDictionary<string, string?>? map)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        if (map is not null && map.TryGetValue(code, out var d) && !string.IsNullOrWhiteSpace(d))
            return d;
        return code;
    }

    /// <summary>
    /// Parses a JSON array (e.g. <c>["01","02"]</c>) — or a single code — and joins the
    /// matching Thai descriptions from <paramref name="map"/>, comma-separated. Any code
    /// not present in the map falls back to the raw code. Returns null for null/empty input.
    ///
    /// Mirrors <c>JsonCodesToThai</c> in <c>LandSectionLoader</c>.
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

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────────

    private sealed class ComparableRow
    {
        public Guid       Id                 { get; init; }
        public string?    ComparableNumber   { get; init; }
        public string?    PropertyType       { get; init; }
        public string?    SurveyName         { get; init; }
        public decimal?   OfferPrice         { get; init; }
        public DateTime?  InfoDateTime       { get; init; }
        public int        MinDisplaySequence { get; init; }
    }

    private sealed class FactorDataRow
    {
        public Guid    FactorId            { get; init; }
        public string? FactorLabel         { get; init; }
        public string? DataType            { get; init; }
        public string? ParameterGroup      { get; init; }
        public Guid    MarketComparableId  { get; init; }
        public string? Value               { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group       { get; init; }
        public string? Code        { get; init; }
        public string? Description { get; init; }
    }
}
