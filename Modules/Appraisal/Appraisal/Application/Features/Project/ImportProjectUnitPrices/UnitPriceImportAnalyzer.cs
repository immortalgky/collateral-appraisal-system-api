using System.Globalization;

namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <param name="Response">What preview returns (and import echoes back).</param>
/// <param name="Matched">unit id -> the sheet row that prices it (one row per unit).</param>
/// <param name="MissingUnitIds">Project units the file has no row for.</param>
public sealed record UnitPriceImportAnalysis(
    UnitPriceImportPreviewResponse Response,
    IReadOnlyDictionary<Guid, UnitPriceSheetRow> Matched,
    IReadOnlyList<Guid> MissingUnitIds);

/// <summary>
/// Pure matcher: pairs sheet rows with project units (key 1, else key 2; trimmed, case-insensitive) and collects blocking errors,
/// compare-only differences and sum checks. Writes nothing.
/// </summary>
public static class UnitPriceImportAnalyzer
{
    /// <param name="currentAppraisalValues">Stored TotalAppraisalValueRounded per unit id (for the preview's "previous").</param>
    public static UnitPriceImportAnalysis Analyze(
        string fileName,
        UnitPriceSheet sheet,
        UnitPriceImportLayout layout,
        IReadOnlyList<UnitSnapshot> units,
        IReadOnlyDictionary<Guid, decimal?> currentAppraisalValues)
    {
        var errors = sheet.Errors.ToList();
        var byKey1 = Index(units, layout.Key1Of);
        var byKey2 = Index(units, layout.Key2Of);

        var firstRowOfUnit = new Dictionary<Guid, int>();
        var matched = new Dictionary<Guid, UnitPriceSheetRow>();
        var rows = new List<UnitPriceImportRow>();
        var diffs = new List<UnitPriceAttributeDiff>();
        int primary = 0, secondary = 0, ignored = 0;

        foreach (var row in sheet.Rows)
        {
            var key = row.DisplayKey;
            var (unit, viaPrimary, error) = Match(row, layout, byKey1, byKey2);

            if (unit is not null && firstRowOfUnit.TryGetValue(unit.Id, out var firstRow))
            {
                error = new(row.RowNumber, key, UnitPriceImportErrorCodes.DuplicateUnit,
                    $"Row {firstRow} already prices the same unit.");
                unit = null;
            }

            // A row with no price at all whose key matches no unit is a note, a signature or a repeated header
            // typed in the key column: ignore it. (Ambiguous or duplicate rows are still reported.)
            if (unit is null && !row.HasPrice && error!.Code == UnitPriceImportErrorCodes.UnitNotFound)
            {
                ignored++;
                continue;
            }

            if (unit is null)
            {
                errors.Add(error!);
                rows.Add(new(row.RowNumber, key ?? "", null, null, row.AppraisalValue));
                continue;
            }

            firstRowOfUnit[unit.Id] = row.RowNumber;
            diffs.AddRange(Compare(row, unit, layout));
            // Key 1 found the unit but the row's key 2 says something else: report it, never block on it.
            if (viaPrimary && row.Key2 is not null
                && !string.Equals(layout.Key2Of(unit), row.Key2, StringComparison.OrdinalIgnoreCase))
                diffs.Add(new UnitPriceAttributeDiff(
                    row.RowNumber, row.DisplayKey!, layout.Key2Header, layout.Key2(unit)?.Trim(), row.Key2));
            // Found by key 2 although the row also carries a key 1 that matches nothing: report that too.
            if (!viaPrimary && row.Key1 is not null)
                diffs.Add(new UnitPriceAttributeDiff(
                    row.RowNumber, row.DisplayKey!, layout.Key1Header, layout.Key1Of(unit), row.Key1));

            // All four price cells blank = "this unit has no price" (a template downloaded from a partly
            // priced project): handled like a unit missing from the file, so it is cleared, not blocking.
            if (!row.HasPrice)
            {
                rows.Add(new(row.RowNumber, key ?? "", unit.Id, currentAppraisalValues.GetValueOrDefault(unit.Id), null));
                continue;
            }

            matched[unit.Id] = row;
            if (viaPrimary) primary++; else secondary++;

            if (row.AppraisalValue is null)
                errors.Add(new(row.RowNumber, key, UnitPriceImportErrorCodes.MissingAppraisalValue,
                    $"'{layout.AppraisalValueHeader}' is empty although other prices are filled in."));
            else
            {
                // The forced-sale price is required once the row is priced; a blank insurance value is stored as null.
                if (row.ForceSellingPrice is null)
                    errors.Add(new(row.RowNumber, key, UnitPriceImportErrorCodes.MissingPrice,
                        $"'{layout.ForceSellingPriceHeader}' is empty for a priced unit."));
            }
            rows.Add(new(row.RowNumber, key ?? "", unit.Id,
                currentAppraisalValues.GetValueOrDefault(unit.Id), row.AppraisalValue));
        }

        var missing = units
            .Where(u => !matched.ContainsKey(u.Id))
            .OrderBy(u => u.SequenceNumber)
            .ToList();

        var totals = new UnitPriceImportTotals(
            matched.Values.Sum(r => r.AppraisalValue ?? 0m),
            matched.Values.Sum(r => r.ForceSellingPrice ?? 0m),
            matched.Values.Sum(r => r.CoverageAmount ?? 0m));

        var sheetTotals = sheet.TotalsRow is { } t
            ? new UnitPriceSheetTotals(t.AppraisalValue, t.ForceSellingPrice, t.CoverageAmount)
            : null;

        var response = new UnitPriceImportPreviewResponse(
            FileName: fileName,
            ProjectType: layout.ProjectTypeName,
            RowCount: sheet.Rows.Count - ignored,
            MatchedCount: matched.Count,
            MatchedByPrimaryKey: primary,
            MatchedBySecondaryKey: secondary,
            UnitsInProject: units.Count,
            Errors: errors.OrderBy(e => e.RowNumber).ToList(),
            AttributeDiffs: diffs,
            UnitsMissingFromFile: missing
                .Select(u => new UnitMissingFromFile(u.Id, layout.Key1Of(u) ?? layout.Key2Of(u))).ToList(),
            Rows: rows,
            Totals: totals,
            SheetTotals: sheetTotals,
            SheetTotalsMatch: SheetTotalsMatch(sheetTotals, totals),
            CanImport: errors.Count == 0 && matched.Count > 0);

        return new UnitPriceImportAnalysis(response, matched, missing.Select(u => u.Id).ToList());
    }

    private static ILookup<string, UnitSnapshot> Index(IEnumerable<UnitSnapshot> units, Func<UnitSnapshot, string?> key) =>
        units
            .Select(u => (Key: key(u), Unit: u))
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .ToLookup(x => x.Key!, x => x.Unit, StringComparer.OrdinalIgnoreCase);

    private static (UnitSnapshot? Unit, bool ViaPrimary, UnitPriceImportError? Error) Match(
        UnitPriceSheetRow row, UnitPriceImportLayout layout,
        ILookup<string, UnitSnapshot> byKey1, ILookup<string, UnitSnapshot> byKey2)
    {
        var key = row.DisplayKey;
        if (key is null)
            return (null, false, new(row.RowNumber, null, UnitPriceImportErrorCodes.UnitNotFound,
                "The row has no unit key. (A subtotal or total row is recognised only when its appraisal value equals " +
                "the sum of the rows it covers.)"));

        var hits1 = row.Key1 is null ? [] : byKey1[row.Key1].ToList();
        var hits2 = row.Key2 is null ? [] : byKey2[row.Key2].ToList();

        // Key 1 found one unit but key 2 names other unit(s) and not that one: the row contradicts itself.
        // (Key 2 matching no unit at all is only a difference, reported by the caller.)
        if (hits1.Count == 1 && hits2.Count > 0 && hits2.All(h => h.Id != hits1[0].Id))
            return (null, false, new(row.RowNumber, key, UnitPriceImportErrorCodes.AmbiguousMatch,
                "The two key columns point at different units."));

        // Key 1 shared by several units (e.g. the same room number in two towers): key 2 can tell them apart.
        if (hits1.Count > 1)
        {
            var narrowed = hits1.Where(h => hits2.Any(x => x.Id == h.Id)).ToList();
            if (narrowed.Count == 1) return (narrowed[0], true, null);

            // Key 2 names unit(s) that are none of the key 1 hits: the row contradicts itself; no tower tie-break.
            if (hits2.Count > 0 && narrowed.Count == 0)
                return (null, false, new(row.RowNumber, key, UnitPriceImportErrorCodes.AmbiguousMatch,
                    "The two key columns point at different units."));

            // Key 2 could not tell them apart (blank, unknown, or still shared): a Condo row can name its tower.
            var candidates = narrowed.Count > 1 ? narrowed : hits1;
            var tower = layout.TowerHeader is { } header && row.Attributes.TryGetValue(header, out var cell) ? cell.Text : null;
            if (tower is not null && layout.Tower is { } towerOf)
            {
                var inTower = candidates
                    .Where(u => string.Equals(towerOf(u)?.Trim(), tower, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (inTower.Count == 1) return (inTower[0], true, null);
            }
        }

        if (hits1.Count > 1 || (hits1.Count == 0 && hits2.Count > 1))
            return (null, false, new(row.RowNumber, key, UnitPriceImportErrorCodes.AmbiguousMatch,
                "The key matches more than one unit."));

        if (hits1.Count == 1) return (hits1[0], true, null);
        if (hits2.Count == 1) return (hits2[0], false, null);

        return (null, false, new(row.RowNumber, key, UnitPriceImportErrorCodes.UnitNotFound,
            "No unit in the project matches this row."));
    }

    private static IEnumerable<UnitPriceAttributeDiff> Compare(
        UnitPriceSheetRow row, UnitSnapshot unit, UnitPriceImportLayout layout)
    {
        foreach (var column in layout.CompareColumns)
        {
            if (!row.Attributes.TryGetValue(column.Header, out var cell)) continue;

            var system = column.Read(unit);
            string? systemText, fileText;
            bool equal;

            if (column.Numeric)
            {
                decimal? systemNumber = system switch
                {
                    int i => i,
                    decimal d => UnitPriceExcelParser.Round(d),
                    _ => null
                };
                equal = cell.Number == systemNumber && (cell.Number is not null || cell.Text is null);
                systemText = Format(systemNumber);
                fileText = cell.Number is not null ? Format(cell.Number) : cell.Text;
            }
            else
            {
                systemText = (system as string)?.Trim();
                if (systemText?.Length == 0) systemText = null;
                fileText = cell.Text;
                equal = string.Equals(systemText, fileText, StringComparison.Ordinal);
            }

            if (!equal)
                yield return new UnitPriceAttributeDiff(row.RowNumber, row.DisplayKey!, column.Header, systemText, fileText);
        }
    }

    private static string? Format(decimal? n) => n?.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Each sheet total that is present must be within 1 baht of the matched-row sum; null when no totals to compare.</summary>
    private static bool? SheetTotalsMatch(UnitPriceSheetTotals? sheet, UnitPriceImportTotals sum)
    {
        if (sheet is null) return null;

        var checks = new List<bool>();
        if (sheet.AppraisalValue is { } a) checks.Add(Math.Abs(a - sum.AppraisalValue) <= 1m);
        if (sheet.ForceSellingPrice is { } f) checks.Add(Math.Abs(f - sum.ForceSellingPrice) <= 1m);
        if (sheet.CoverageAmount is { } c) checks.Add(Math.Abs(c - sum.CoverageAmount) <= 1m);

        return checks.Count == 0 ? null : checks.All(x => x);
    }
}
