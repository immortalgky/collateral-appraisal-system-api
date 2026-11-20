using Reporting.Application.Formatting;
using System.Data;
using System.Text.Json;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "ข้อมูลตลาดเปรียบเทียบ" (Comparison Information) section — FSD §2.1.2.8.
///
/// Navigation used (schema: appraisal.*):
///   PropertyGroups (AppraisalId) → PricingAnalysis (SubjectType=0, AnchorId) → PricingAnalysisApproaches
///     → PricingAnalysisMethods → PricingComparableLinks (MarketComparableId, DisplaySequence)
///       → MarketComparables mc (IsDeleted = 0) → MarketComparableData mcd (MarketComparableId, FactorId, Value)
///         → MarketComparableFactors mcf (IsActive = 1) → MarketComparableFactorTranslations (Language = 'th')
///
/// The section lists, per property group, the comparables that group's pricing methods link (a comparable used by
/// two groups appears under both). Column numbers "ข้อมูล N" mirror the group's first pricing grid (see <see cref="Numbering"/>).
/// Per group the comparables are split into one <see cref="ComparisonTable"/> per <c>MarketComparable.PropertyType</c>
/// (first appearance in the group's column order), each cut into chunks of 3 columns, and every table carries the
/// group's subject column (see <see cref="ComparisonSubject"/>).
///
/// The printed rows of a table (<see cref="BuildFsdRows"/>) follow the factors the group's comparative methods (WQS / Sale Grid /
/// Direct Comparison) use, then the static rows; a group with no such factors prints no table, and with no group left the section is empty.
///
/// Fixed trailing rows of <see cref="ComparisonTable.Rows"/> (always appended after the EAV factor rows of each table):
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

    /// <summary>Comparable columns per printed table (3 keeps the factor values readable).</summary>
    private const int ChunkSize = 3;

    /// <summary>Key of the subject's building photo (a file:// URI) in the dictionary <see cref="ComparisonSubject"/> returns.</summary>
    internal const string SubjectPhotoKey = "photo";

    /// <summary>
    /// Loads all <see cref="ComparisonSection"/>s for the given <paramref name="appraisalId"/>.
    /// Returns a list of one section holding, per property group, the comparables its pricing methods link.
    /// Returns an empty list when no pricing-method comparable links exist.
    /// </summary>
    /// <param name="connection">An open <see cref="IDbConnection"/> (Dapper, read-only).</param>
    /// <param name="appraisalId">The appraisal to load comparables for.</param>
    /// <param name="ct">Cancellation token (not forwarded to Dapper; cancellation is
    ///   cooperative at query boundaries).</param>
    /// <param name="subjectValues">group number → FactorCode → the subject's value for that property group (see <see cref="ComparisonSubject"/>);
    ///   null leaves the subject column out.</param>
    /// <param name="toApplicationTime">UTC → the application's time zone, for the Date factor values (required: a Date value must never be read in the server's zone).</param>
    public static async Task<IReadOnlyList<ComparisonSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        Func<DateTime, DateTime> toApplicationTime,
        Func<int, IReadOnlyDictionary<string, string?>>? subjectValues = null,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: the comparables each property group uses in its pricing methods ──
        //
        // Links are per group (PropertyGroups → PricingAnalysis(SubjectType 0) → approaches → methods → PricingComparableLinks),
        // so the section is organised by group. A comparable used by two groups appears under both.
        const string linksSql = """
            SELECT pg.GroupNumber, pam.Id AS MethodId, pcl.MarketComparableId, pcl.DisplaySequence
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id
            JOIN appraisal.PricingComparableLinks pcl ON pcl.PricingMethodId = pam.Id
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber, paa.IsSelected DESC, CASE WHEN paa.CreatedAt IS NULL THEN 1 ELSE 0 END, paa.CreatedAt, paa.ApproachType, paa.Id, pam.Id, pcl.DisplaySequence, pcl.MarketComparableId
            """;
        var allLinkRows = (await connection.QueryAsync<LinkRow>(linksSql, p)).ToList();

        // Only the methods the book prints a grid for (the picker's rule: selected ones, else the ones with a value). A comparable linked
        // to an unselected alternative method appears in no printed grid, so it must not get a number or a column here.
        var printedMethods = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["WQS", "SaleGrid", "DirectComparison"], ct);
        var printedMethodIds = printedMethods.Select(m => m.PricingMethodId).ToHashSet();
        var linkRows = allLinkRows.Where(l => printedMethodIds.Contains(l.MethodId)).ToList();
        if (linkRows.Count == 0)
            return [];

        var groupNumbers = (await connection.QueryAsync<int>(
            "SELECT GroupNumber FROM appraisal.PropertyGroups WHERE AppraisalId = @AppraisalId ORDER BY GroupNumber", p)).ToList();

        // The order the book prints the grids of a group in (book-flow-methods.html): the used approaches before the reference ones, and inside each
        // pass every WQS before the Sale Grid / Direct Comparison ones, each kind in the picker's (creation) order. Only methods that have link
        // rows count: a printed method with no comparables is no grid column to follow. The first of them fixes the numbering, and comparables only
        // later grids add are numbered on in that same order (never by method Guid).
        var linkedMethodIds = linkRows.Select(l => l.MethodId).ToHashSet();
        var printOrderByGroup = printedMethods
            .Where(m => linkedMethodIds.Contains(m.PricingMethodId))
            .OrderBy(m => m.Context.IsReference)
            .ThenBy(m => string.Equals(m.MethodType, "WQS", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .GroupBy(m => m.GroupNumber)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(m => m.PricingMethodId).ToList());

        // groupNumber → [(comparableId, "ข้อมูล N")], in column order.
        var numberingByGroup = new Dictionary<int, IReadOnlyList<(Guid Id, int Number)>>();
        foreach (var g in linkRows.GroupBy(l => l.GroupNumber))
            numberingByGroup[g.Key] = Numbering(g.ToList(), printOrderByGroup.GetValueOrDefault(g.Key) ?? []);

        // The factors each group's comparative methods (WQS / Sale Grid / Direct Comparison) use, for the factor rows of §8.
        var methodFactorsByGroup = await LoadMethodFactorsAsync(connection, appraisalId, ct);

        var usedIds = numberingByGroup.Values.SelectMany(v => v.Select(x => x.Id)).Distinct().ToList();

        // Columns sourced (appraisal.MarketComparables, IsDeleted = 0): Id, ComparableNumber, PropertyType, SurveyName,
        // OfferPrice, InfoDateTime, SalePrice, SaleDate, SourceInfo, Notes, Latitude, Longitude.
        const string comparablesSql = """
            SELECT mc.Id, mc.ComparableNumber, mc.PropertyType, mc.SurveyName, mc.OfferPrice, mc.InfoDateTime,
                   mc.SalePrice, mc.SaleDate, mc.SourceInfo, mc.Notes, mc.Latitude, mc.Longitude
            FROM appraisal.MarketComparables mc
            WHERE mc.Id IN @Ids AND mc.IsDeleted = 0
            """;
        var comparableRows = (await connection.QueryAsync<ComparableRow>(comparablesSql, new { Ids = usedIds })).ToList();
        var comparableById = comparableRows.ToDictionary(r => r.Id);

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
                mcf.FactorCode,
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

        // ── Q4: ONE photo per comparable (the "ภาพถ่าย" row): the first by the appraiser's order ──────────────
        // MarketComparableImages.DisplaySequence, then CreatedAt, then Id (v7 Guids are not time-ordered, so Id only
        // breaks the last tie). File and mime resolve as AppendixSectionLoader does: document.Documents first, the gallery's
        // denormalised columns as fallback; images only. One query for every comparable of the book.
        const string photosSql = """
            SELECT MarketComparableId, FilePath
            FROM (
                SELECT mi.MarketComparableId,
                       COALESCE(dg.StoragePath, g.FilePath) AS FilePath,
                       ROW_NUMBER() OVER (PARTITION BY mi.MarketComparableId
                                          ORDER BY mi.DisplaySequence, mi.CreatedAt, mi.Id) AS Rn
                FROM appraisal.MarketComparableImages mi
                JOIN appraisal.AppraisalGallery g ON g.Id = mi.GalleryPhotoId
                LEFT JOIN document.Documents dg ON dg.Id = g.DocumentId
                WHERE mi.MarketComparableId IN @ComparableIds
                  -- dg is the unfiltered join: g.FilePath is only the fallback for a photo with no document row; a deleted/deactivated
                  -- document drops the photo BEFORE the ranking, so Rn = 1 is the next valid image
                  AND (dg.Id IS NULL OR (dg.IsActive = 1 AND dg.IsDeleted = 0))
                  AND COALESCE(dg.MimeType, g.MimeType) LIKE 'image/%'
                  AND COALESCE(dg.StoragePath, g.FilePath) IS NOT NULL
            ) x
            WHERE Rn = 1
            """;
        var photosByComparable = (await connection.QueryAsync<PhotoRow>(photosSql, factorParams))
            .GroupBy(r => r.MarketComparableId)
            .Select(g => (Id: g.Key, Src: AppendixSectionLoader.ToFileUri(g.First().FilePath)))
            .Where(x => x.Src is not null)
            .ToDictionary(x => x.Id, x => (IReadOnlyList<string>)[x.Src!]);

        // The subject's fallback photo when no real building of the group has one: any property of the group with an image —
        // a thumbnail first, then by position in the group, SequenceNumber, LinkedAt. Same mime/path rules as the comparables'
        // photos. One query for all groups.
        var groupPhotoFallback = subjectValues is null
            ? new Dictionary<int, string?>()
            : (await connection.QueryAsync<(int GroupNumber, string? FilePath)>("""
                SELECT GroupNumber, FilePath
                FROM (
                    SELECT pg.GroupNumber,
                           COALESCE(d.StoragePath, g.FilePath) AS FilePath,
                           ROW_NUMBER() OVER (PARTITION BY pg.GroupNumber
                                              ORDER BY ppm.IsThumbnail DESC, pgi.SequenceInGroup, ppm.SequenceNumber, ppm.LinkedAt, ppm.Id) AS Rn
                    FROM appraisal.PropertyGroups pg
                    JOIN appraisal.PropertyGroupItems pgi ON pgi.PropertyGroupId = pg.Id
                    JOIN appraisal.PropertyPhotoMappings ppm ON ppm.AppraisalPropertyId = pgi.AppraisalPropertyId
                    JOIN appraisal.AppraisalGallery g ON g.Id = ppm.GalleryPhotoId
                    LEFT JOIN document.Documents d ON d.Id = g.DocumentId
                    WHERE pg.AppraisalId = @AppraisalId
                      AND (d.Id IS NULL OR (d.IsActive = 1 AND d.IsDeleted = 0))   -- unfiltered join: see Q4
                      AND COALESCE(d.MimeType, g.MimeType) LIKE 'image/%'
                      AND COALESCE(d.StoragePath, g.FilePath) IS NOT NULL
                ) x
                WHERE Rn = 1
                """, p)).ToDictionary(r => r.GroupNumber, r => AppendixSectionLoader.ToFileUri(r.FilePath));

        // ── Tables: per group, per property type (first appearance in the group's column order), 3 comparables a table ──
        var tables = new List<ComparisonTable>();
        foreach (var (groupNumber, numbering) in numberingByGroup.OrderBy(kv => kv.Key))
        {
            // A group with no comparative method, or whose methods list no factor, prints no table: the rows come from those factors.
            if (methodFactorsByGroup.GetValueOrDefault(groupNumber) is not { Count: > 0 } groupFactors)
                continue;
            var members = numbering.Where(n => comparableById.ContainsKey(n.Id))
                .Select(n => (Row: comparableById[n.Id], n.Number)).ToList();
            var numberOf = members.ToDictionary(m => m.Row.Id, m => m.Number);
            var subject = subjectValues?.Invoke(groupNumber);
            var subjectPhoto = subject?.GetValueOrDefault(SubjectPhotoKey) ?? groupPhotoFallback.GetValueOrDefault(groupNumber);

            foreach (var type in members.GroupBy(m => m.Row.PropertyType ?? string.Empty))
            {
                var typeMembers = type.Select(m => m.Row).ToList();
                for (var offset = 0; offset < typeMembers.Count; offset += ChunkSize)
                    tables.Add(BuildTable(typeMembers.Skip(offset).Take(ChunkSize).ToList(), numberOf, subject, groupNumber, subjectPhoto,
                        factorDataRows, paramMaps, photosByComparable, groupFactors, toApplicationTime));
            }
        }

        // No group has comparative factors: no section at all (the templates drop the heading and the contents entry).
        if (tables.Count == 0)
            return [];

        // Wrap in a list of 1. The comparison section aggregates all property groups'
        // comparables into a single section — GroupNumber = 0, GroupName = null.
        return [new ComparisonSection { Tables = tables, MultiGroup = groupNumbers.Count > 1 }];
    }

    // ── Numbering ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The "ข้อมูล N" of each comparable a group uses, in column order, mirroring the grids the book prints: N is the 1-based POSITION in the
    /// first printed grid's columns (every grid header prints "ตลาด {position}"), then the comparables only later grids link follow, grid by
    /// grid in <paramref name="printOrder"/> (the order the book prints them in). <paramref name="groupLinks"/> must come in the order SQL
    /// returned them (ORDER BY ... DisplaySequence, MarketComparableId): the grids read their columns with the same order, and re-sorting here
    /// in C# would break ties differently (C# and SQL Server order Guids differently). A method missing from the order comes last.
    /// </summary>
    internal static IReadOnlyList<(Guid Id, int Number)> Numbering(IReadOnlyList<LinkRow> groupLinks, IReadOnlyList<Guid> printOrder)
    {
        var result = new List<(Guid, int)>();
        var seen = new HashSet<Guid>();
        var methods = printOrder.Concat(groupLinks.Select(l => l.MethodId).Where(m => !printOrder.Contains(m)).Distinct());
        foreach (var method in methods)
            foreach (var l in groupLinks.Where(l => l.MethodId == method))
                if (seen.Add(l.MarketComparableId))
                    result.Add((l.MarketComparableId, result.Count + 1));
        return result;
    }

    // ── Table builder ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds one <see cref="ComparisonTable"/> for a single property-type group:
    /// renumbered "ข้อมูล {n}" headers, pivoted factor rows (only factors present in this
    /// group's comparables, coded values resolved to Thai), and the fixed trailing rows.
    /// </summary>
    private static ComparisonTable BuildTable(
        IReadOnlyList<ComparableRow> comparables,
        IReadOnlyDictionary<Guid, int> numberOf,
        IReadOnlyDictionary<string, string?>? subject,
        int groupNumber,
        string? subjectPhoto,
        IReadOnlyList<FactorDataRow> factorDataRows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> photosByComparable,
        IReadOnlyList<FactorRef> methodFactors,
        Func<DateTime, DateTime> toApplicationTime)
    {
        var memberIds = comparables.Select(c => c.Id).ToHashSet();

        // Column index local to this table.
        var columnIndex = comparables
            .Select((c, i) => (c.Id, Index: i))
            .ToDictionary(x => x.Id, x => x.Index);

        var headers = comparables
            .Select(c => $"ข้อมูล {numberOf[c.Id]}")
            .ToList();

        // Pivot EAV rows (for this group's comparables only) into a factor-keyed dictionary.
        var factorOrder = new List<Guid>();
        var factorLabels = new Dictionary<Guid, string?>();
        var factorCodes = new Dictionary<Guid, string?>();
        var factorValues = new Dictionary<Guid, string?[]>();

        foreach (var row in factorDataRows)
        {
            if (!memberIds.Contains(row.MarketComparableId))
                continue;

            if (!factorValues.ContainsKey(row.FactorId))
            {
                factorOrder.Add(row.FactorId);
                factorLabels[row.FactorId] = row.FactorLabel;
                factorCodes[row.FactorId] = row.FactorCode;
                factorValues[row.FactorId] = new string?[comparables.Count];
            }

            if (columnIndex.TryGetValue(row.MarketComparableId, out var colIdx))
                factorValues[row.FactorId][colIdx] = ResolveValue(row, paramMaps, toApplicationTime);
        }

        var rows = new List<ComparisonFactorRow>(factorOrder.Count + 3);

        foreach (var factorId in factorOrder)
        {
            rows.Add(new ComparisonFactorRow
            {
                FactorName = factorLabels[factorId],
                FactorCode = factorCodes[factorId],
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

        // วันที่ลงข้อมูล — mc.InfoDateTime (date only, printed the way the whole book prints dates: ThaiDateFormatter)
        rows.Add(BuildFixedRow(
            "วันที่ลงข้อมูล",
            comparables,
            r => r.InfoDateTime.HasValue
                ? ThaiDateFormatter.Long(r.InfoDateTime.Value)
                : null));

        // แหล่งที่มา — mc.SurveyName
        rows.Add(BuildFixedRow(
            "แหล่งที่มา",
            comparables,
            r => r.SurveyName));

        // The rows below print only when at least one comparable has a value, so a table whose data
        // never recorded them does not grow empty rows.
        AddRowIfAny(rows, "ราคาซื้อขาย", comparables, r => r.SalePrice?.ToString("N0"));
        AddRowIfAny(rows, "วันที่ซื้อขาย", comparables, r => ThaiDateFormatter.Long(r.SaleDate));
        AddRowIfAny(rows, "พิกัด", comparables, r => r.Latitude.HasValue && r.Longitude.HasValue
            ? $"{r.Latitude:F6}, {r.Longitude:F6}" : null);
        AddRowIfAny(rows, "ผู้ให้ข้อมูล / เบอร์ติดต่อ", comparables, r => r.SourceInfo);
        AddRowIfAny(rows, "หมายเหตุ", comparables, r => r.Notes);

        var propertyTypeMap = paramMaps.GetValueOrDefault(PropertyTypeGroup);
        var fsdRows = BuildFsdRows(comparables, rows, subject, methodFactors,
            comparables.Select(c => ResolveCode(c.PropertyType, propertyTypeMap)).ToList());

        var photos = comparables
            .Select(c => photosByComparable.GetValueOrDefault(c.Id) ?? (IReadOnlyList<string>)[])
            .ToList();

        // Heading — Thai description of the property type (codes shared across the group).
        var propertyTypeCode = comparables[0].PropertyType;
        var propertyTypeLabel = ResolveCode(propertyTypeCode, paramMaps.GetValueOrDefault(PropertyTypeGroup));

        return new ComparisonTable
        {
            PropertyTypeLabel = propertyTypeLabel,
            ComparableHeaders = headers,
            Rows              = rows,
            FsdRows           = fsdRows,
            SubjectHeaders    = subject is null ? [] : ["ทรัพย์สินที่ประเมิน"],
            GroupNumber       = groupNumber,
            SubjectPhoto      = subjectPhoto,
            Photos            = photos.Any(p => p.Count > 0) ? photos : []
        };
    }

    // ── §8 rows ─────────────────────────────────────────────────────────────────────

    /// <summary>A factor a pricing method uses: its FactorCode (the key to the comparables' values and the subject) and display name.</summary>
    internal sealed record FactorRef(string Code, string? Label);

    /// <summary>
    /// The factors of a group's methods, unioned and de-duplicated by FactorCode: the first method's order first, then any
    /// factor only a later method uses, each method's own factors in the order that method lists them.
    /// </summary>
    internal static IReadOnlyList<FactorRef> UnionFactors(IEnumerable<IReadOnlyList<FactorRef>> perMethod)
    {
        var result = new List<FactorRef>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in perMethod)
            foreach (var f in method)
                if (!string.IsNullOrWhiteSpace(f.Code) && seen.Add(f.Code))
                    result.Add(f);
        return result;
    }

    /// <summary>
    /// groupNumber → the factors its comparative methods use. Where each method stores them: Sale Grid and Direct Comparison in
    /// PricingComparativeFactors (IsSelectedForScoring = 1, by DisplaySequence); WQS in PricingFactorScores (the distinct factors
    /// by their lowest DisplaySequence) — the same tables SaleGridSectionLoader and WqsSectionLoader print their factor rows from.
    /// Methods are the ones the book prints (PricingMethodPicker), in its order.
    /// </summary>
    private static async Task<IReadOnlyDictionary<int, IReadOnlyList<FactorRef>>> LoadMethodFactorsAsync(
        IDbConnection connection, Guid appraisalId, CancellationToken ct)
    {
        var methods = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["WQS", "SaleGrid", "DirectComparison"], ct);
        if (methods.Count == 0)
            return new Dictionary<int, IReadOnlyList<FactorRef>>();

        const string names = """
            LEFT JOIN appraisal.MarketComparableFactorTranslations th ON th.MarketComparableFactorId = mcf.Id AND th.Language = 'th'
            LEFT JOIN appraisal.MarketComparableFactorTranslations en ON en.MarketComparableFactorId = mcf.Id AND en.Language = 'en'
            """;
        var gridIds = methods.Where(m => !string.Equals(m.MethodType, "WQS", StringComparison.OrdinalIgnoreCase)).Select(m => m.PricingMethodId).ToList();
        var wqsIds = methods.Where(m => string.Equals(m.MethodType, "WQS", StringComparison.OrdinalIgnoreCase)).Select(m => m.PricingMethodId).ToList();

        var rows = new List<MethodFactorRow>();
        if (gridIds.Count > 0)
            rows.AddRange(await connection.QueryAsync<MethodFactorRow>($"""
                SELECT pcf.PricingMethodId AS MethodId, mcf.FactorCode, COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS Label, pcf.DisplaySequence AS Seq
                FROM appraisal.PricingComparativeFactors pcf
                JOIN appraisal.MarketComparableFactors mcf ON mcf.Id = pcf.FactorId
                {names}
                WHERE pcf.PricingMethodId IN @Ids AND pcf.IsSelectedForScoring = 1
                """, new { Ids = gridIds }));
        if (wqsIds.Count > 0)
            rows.AddRange(await connection.QueryAsync<MethodFactorRow>($"""
                SELECT ps.PricingMethodId AS MethodId, mcf.FactorCode, COALESCE(th.FactorName, en.FactorName, mcf.FieldName) AS Label, MIN(ps.DisplaySequence) AS Seq
                FROM appraisal.PricingFactorScores ps
                JOIN appraisal.MarketComparableFactors mcf ON mcf.Id = ps.FactorId
                {names}
                WHERE ps.PricingMethodId IN @Ids
                GROUP BY ps.PricingMethodId, mcf.Id, mcf.FactorCode, th.FactorName, en.FactorName, mcf.FieldName
                """, new { Ids = wqsIds }));

        var byMethod = rows.GroupBy(r => r.MethodId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<FactorRef>)g.OrderBy(r => r.Seq).ThenBy(r => r.FactorCode, StringComparer.Ordinal)
                .Where(r => !string.IsNullOrWhiteSpace(r.FactorCode)).Select(r => new FactorRef(r.FactorCode!, r.Label)).ToList());

        return methods.GroupBy(m => m.GroupNumber).ToDictionary(
            g => g.Key,
            g => UnionFactors(g.Select(m => byMethod.GetValueOrDefault(m.PricingMethodId) ?? [])));
    }

    /// <summary>
    /// The rows the book prints for one table (FSD 2.1.2.8 layout). With the group's method factors: one row per factor in the
    /// methods' order (the rai / ngan / wa factors merged into one เนื้อที่ดิน row at the first of them), then the static rows,
    /// each only when some comparable has a value. A group without method factors gets no table at all (see LoadAllAsync).
    /// </summary>
    internal static IReadOnlyList<ComparisonFactorRow> BuildFsdRows(
        IReadOnlyList<ComparableRow> comparables,
        IReadOnlyList<ComparisonFactorRow> factorRows,
        IReadOnlyDictionary<string, string?>? subject,
        IReadOnlyList<FactorRef> methodFactors,
        IReadOnlyList<string?>? propertyTypeLabels = null)
    {
        var byCode = factorRows.Where(r => r.FactorCode is not null)
            .GroupBy(r => r.FactorCode!)
            .ToDictionary(g => g.Key, g => g.First().Values);
        IReadOnlyList<string?> Col(string code) =>
            byCode.GetValueOrDefault(code) ?? new string?[comparables.Count];
        // code = the MarketComparableFactors.FactorCode behind the row; it keys the subject's value (null → "-").
        ComparisonFactorRow Fsd(string label, IReadOnlyList<string?> values, bool bold = false, string? code = null) =>
            new()
            {
                FactorName = label, Values = values, Bold = bold, FactorCode = code,
                SubjectValues = subject is null ? [] : [code is null ? null : subject.GetValueOrDefault(code)]
            };

        // เนื้อที่ดิน: the rai / ngan / wa factors read as one figure when any of them is filled in.
        IReadOnlyList<string?> LandArea()
        {
            var rai = Col("43"); var ngan = Col("44"); var wa = Col("45");
            if (!rai.Concat(ngan).Concat(wa).Any(v => !string.IsNullOrWhiteSpace(v)))
                return Col("02");
            return Enumerable.Range(0, comparables.Count)
                .Select(i => $"{(string.IsNullOrWhiteSpace(rai[i]) ? "0" : rai[i])}-{(string.IsNullOrWhiteSpace(ngan[i]) ? "0" : ngan[i])}-{(string.IsNullOrWhiteSpace(wa[i]) ? "0" : wa[i])}")
                .Cast<string?>().ToList();
        }

        IReadOnlyList<string?> OfferPrice() => comparables.Select(r => r.OfferPrice?.ToString("N0")).ToArray();
        IReadOnlyList<string?> InfoDate() => comparables.Select(r => ThaiDateFormatter.Long(r.InfoDateTime)).ToArray();
        IReadOnlyList<string?> Notes() => comparables.Select(r => r.Notes).ToArray();

        // ประเภททรัพย์สิน: always the first row, bold, even when no method uses factor 01. Each comparable's own property type
        // (MarketComparables.PropertyType, decoded to Thai) when it has one, else its factor-01 value; the subject's is the appraised
        // property's type text. Factor 01 itself is not printed again below.
        var typeValues = Enumerable.Range(0, comparables.Count)
            .Select(i => propertyTypeLabels is not null && !string.IsNullOrWhiteSpace(propertyTypeLabels[i]) ? propertyTypeLabels[i] : Col("01")[i])
            .ToArray();
        var rows = new List<ComparisonFactorRow> { Fsd("ประเภททรัพย์สิน", typeValues, bold: true, code: "01") };
        HashSet<string> landCodes = ["02", "43", "44", "45"];
        var landDone = false;
        foreach (var f in methodFactors)
        {
            if (f.Code == "01")
                continue;
            if (landCodes.Contains(f.Code))
            {
                if (!landDone) rows.Add(Fsd("เนื้อที่ดิน", LandArea(), code: "02"));
                landDone = true;
                continue;
            }
            rows.Add(Fsd(string.IsNullOrWhiteSpace(f.Label) ? f.Code : f.Label!, Col(f.Code), code: f.Code));
        }

        void IfAny(string label, IReadOnlyList<string?> values)
        {
            if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
                rows.Add(Fsd(label, values));
        }

        IfAny("ราคาเสนอขาย", OfferPrice());
        IfAny("ราคาซื้อขาย", comparables.Select(r => r.SalePrice?.ToString("N0")).ToArray());
        IfAny("วันที่ซื้อขาย", comparables.Select(r => ThaiDateFormatter.Long(r.SaleDate)).ToArray());
        IfAny("วันที่ได้รับข้อมูล", InfoDate());
        // The source is SourceInfo (e.g. "ป้ายประกาศ"); SurveyName is only a survey / market name ("ตลาด 1") and is not printed.
        IfAny("แหล่งที่มาข้อมูล", comparables.Select(r => r.SourceInfo).ToArray());
        IfAny("หมายเหตุ", Notes());
        return rows;
    }

    // ── Value resolution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a factor cell value: parameter-backed DataTypes (Dropdown/Radio/Checkbox/
    /// CheckboxGroup) with a ParameterGroup are mapped code→Thai (JSON-array aware); all other
    /// DataTypes (Text/Numeric/Date) pass through unchanged.
    /// </summary>
    private static string? ResolveValue(
        FactorDataRow row,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> paramMaps,
        Func<DateTime, DateTime> toApplicationTime)
    {
        if (string.Equals(row.DataType, "Date", StringComparison.OrdinalIgnoreCase))
            return PricingGridCommon.DateFactorText(row.Value, toApplicationTime);

        if (!IsParameterBacked(row.DataType) || string.IsNullOrWhiteSpace(row.ParameterGroup))
            return row.Value;

        return LandSectionLoader.JsonCodesToThai(row.Value, paramMaps.GetValueOrDefault(row.ParameterGroup) ?? EmptyMap);
    }

    /// <summary>
    /// True for factor DataTypes whose stored value is a parameter code (or JSON array of
    /// codes) rather than free text. DataType is persisted as the enum's string name.
    /// </summary>
    private static bool IsParameterBacked(string? dataType) => dataType is
        "Dropdown" or "Radio" or "Checkbox" or "CheckboxGroup";

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void AddRowIfAny(
        List<ComparisonFactorRow> rows,
        string label,
        IReadOnlyList<ComparableRow> comparables,
        Func<ComparableRow, string?> valueSelector)
    {
        var values = comparables.Select(valueSelector).ToArray();
        if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
            rows.Add(new ComparisonFactorRow { FactorName = label, Values = values });
    }

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
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>>
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

    // ── Private Dapper flat DTOs ──────────────────────────────────────────────────

    internal sealed class ComparableRow
    {
        public Guid       Id                 { get; init; }
        public string?    ComparableNumber   { get; init; }
        public string?    PropertyType       { get; init; }
        public string?    SurveyName         { get; init; }
        public decimal?   OfferPrice         { get; init; }
        public DateTime?  InfoDateTime       { get; init; }
        public decimal?   SalePrice          { get; init; }
        public DateTime?  SaleDate           { get; init; }
        public string?    SourceInfo         { get; init; }
        public string?    Notes              { get; init; }
        public decimal?   Latitude           { get; init; }
        public decimal?   Longitude          { get; init; }
    }

    internal sealed class LinkRow
    {
        public int  GroupNumber        { get; init; }
        public Guid MethodId           { get; init; }
        public Guid MarketComparableId { get; init; }
        public int  DisplaySequence    { get; init; }
    }

    private sealed class MethodFactorRow
    {
        public Guid    MethodId   { get; init; }
        public string? FactorCode { get; init; }
        public string? Label      { get; init; }
        public int     Seq        { get; init; }
    }

    private sealed class PhotoRow
    {
        public Guid    MarketComparableId { get; init; }
        public string? FilePath           { get; init; }
    }

    private sealed class FactorDataRow
    {
        public Guid    FactorId            { get; init; }
        public string? FactorCode          { get; init; }
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
