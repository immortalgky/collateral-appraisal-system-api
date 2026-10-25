using System.Globalization;
using Appraisal.Application.Features.Project.UploadProjectUnits;
using ClosedXML.Excel;

namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>A compare-only cell: trimmed text (null when blank) and its number when it reads as one.</summary>
public sealed record SheetCell(string? Text, decimal? Number);

public sealed record UnitPriceSheetRow(
    int RowNumber,
    string? Key1,
    string? Key2,
    IReadOnlyDictionary<string, SheetCell> Attributes,
    decimal? StandardPrice,
    decimal? AppraisalValue,
    decimal? ForceSellingPrice,
    decimal? CoverageAmount)
{
    public string? DisplayKey => Key1 ?? Key2;

    public bool HasPrice =>
        StandardPrice.HasValue || AppraisalValue.HasValue || ForceSellingPrice.HasValue || CoverageAmount.HasValue;
}

/// <param name="Rows">Data rows, totals row excluded.</param>
/// <param name="TotalsRow">The trailing row with prices but no unit key, if the sheet has one.</param>
/// <param name="Errors">Header and number errors found while reading.</param>
public sealed record UnitPriceSheet(
    IReadOnlyList<UnitPriceSheetRow> Rows,
    UnitPriceSheetRow? TotalsRow,
    IReadOnlyList<UnitPriceImportError> Errors);

/// <summary>
/// Pure reader of the price-list workbook: first worksheet, row 1 = headers (case/whitespace-insensitive),
/// fully blank rows skipped, numbers read from numeric cells or text with commas and rounded to 2 dp
/// (bank sheets carry float noise). Parsing rules follow Database/Scripts/Maintenance/PatchProjectUnitPrices_*.sql.
/// </summary>
public static class UnitPriceExcelParser
{
    public const int MaxRows = 10_000;

    // 100 billion baht per cell: far above any price, and 10,000 rows of it still sum inside decimal(18,2).
    // Larger numbers are InvalidNumber, never a failed save.
    private const decimal MaxNumber = 1e11m;

    /// <summary>
    /// Rejects an unreadable workbook (400) without needing the project type, so a handler can call it before
    /// it touches the database.
    /// </summary>
    public static void EnsureReadable(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = Open(stream);
    }

    private static XLWorkbook Open(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            throw new BadRequestException("The file is not a readable .xlsx workbook.");
        }

        if (!workbook.Worksheets.Any())
        {
            workbook.Dispose();
            throw new BadRequestException("The workbook has no worksheet.");
        }

        return workbook;
    }

    public static UnitPriceSheet Parse(Stream stream, UnitPriceImportLayout layout)
    {
        using (var workbook = Open(stream))
        {
            var ws = workbook.Worksheets.First();
            var headers = ProjectUnitExcelParser.BuildHeaderMap(ws);

            var key1Col = ProjectUnitExcelParser.TryResolve(headers, layout.Key1Header);
            var key2Col = ProjectUnitExcelParser.TryResolve(headers, layout.Key2Header);
            var standardCol = ProjectUnitExcelParser.TryResolve(headers, layout.StandardPriceHeader);
            var appraisalCol = ProjectUnitExcelParser.TryResolve(headers, layout.AppraisalValueHeader);
            var forceCol = ProjectUnitExcelParser.TryResolve(headers, layout.ForceSellingPriceHeader);
            var coverageCol = layout.CoverageHeader is null ? null : ProjectUnitExcelParser.TryResolve(headers, layout.CoverageHeader);

            var errors = new List<UnitPriceImportError>();
            if (key1Col is null && key2Col is null)
                errors.Add(new(1, null, UnitPriceImportErrorCodes.MissingHeader,
                    $"At least one key column is required: '{layout.Key1Header}' or '{layout.Key2Header}'."));
            // Appraisal value, forced-sale price and insurance value are all required columns.
            foreach (var (col, header) in new[]
                     {
                         (appraisalCol, layout.AppraisalValueHeader),
                         (forceCol, layout.ForceSellingPriceHeader),
                         (coverageCol, layout.CoverageHeader)
                     })
            {
                if (col is null && header is not null)
                    errors.Add(new(1, null, UnitPriceImportErrorCodes.MissingHeader,
                        $"Required column '{header}' is missing."));
            }
            if (errors.Count > 0)
                return new UnitPriceSheet([], null, errors);

            var compareCols = layout.CompareColumns
                .Select(c => (c.Header, Col: ProjectUnitExcelParser.TryResolve(headers, c.Header)))
                .Where(x => x.Col.HasValue)
                .ToList();

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxRows)
                throw new BadRequestException($"Too many rows. Maximum allowed is {MaxRows}.");

            var rows = new List<UnitPriceSheetRow>();
            // Number errors are kept per row and raised only for rows that end up as data: a footer note with
            // text in a price column is ignored, not blocking.
            var rowErrors = new Dictionary<int, List<UnitPriceImportError>>();
            for (var r = 2; r <= lastRow; r++)
            {
                var key1 = key1Col is { } c1 ? CleanKey(ws.Cell(r, c1).GetString()) : null;
                var key2 = key2Col is { } c2 ? CleanKey(ws.Cell(r, c2).GetString()) : null;
                var displayKey = key1 ?? key2;

                var thisRowErrors = new List<UnitPriceImportError>();
                var standard = ReadPrice(ws, r, standardCol, layout.StandardPriceHeader, displayKey, thisRowErrors, out var any1);
                var appraisal = ReadPrice(ws, r, appraisalCol, layout.AppraisalValueHeader, displayKey, thisRowErrors, out var any2);
                var force = ReadPrice(ws, r, forceCol, layout.ForceSellingPriceHeader, displayKey, thisRowErrors, out var any3);
                var coverage = ReadPrice(ws, r, coverageCol, layout.CoverageHeader, displayKey, thisRowErrors, out var any4);

                var attributes = new Dictionary<string, SheetCell>();
                foreach (var (header, col) in compareCols)
                    attributes[header] = ReadAttribute(ws.Cell(r, col!.Value));

                var blank = displayKey is null && !(any1 || any2 || any3 || any4)
                            && attributes.Values.All(a => a.Text is null);
                if (blank) continue;

                rows.Add(new UnitPriceSheetRow(r, key1, key2, attributes, standard, appraisal, force, coverage));
                rowErrors[r] = thisRowErrors;
            }

            var (dataRows, totals) = SplitSummaryRows(rows);
            errors.AddRange(dataRows.SelectMany(d => rowErrors[d.RowNumber]));
            return new UnitPriceSheet(dataRows, totals, errors);
        }
    }

    private static readonly string[] DashOnly = ["-", "\u2013", "\u2014"];   // hyphen, en dash, em dash

    private static readonly string[] SummaryLabels = ["รวม", "total"];

    /// <summary>
    /// Separates summary rows (tower subtotals, the grand total, notes) from unit rows.
    /// A candidate has empty key cells, or ANY key cell containing "รวม" / "total" (e.g. "ยอดรวม", "Grand Total"). A candidate with no
    /// price at all is ignored (notes, signatures, headings). One whose appraisal value is within 1 baht of the
    /// sum of the data rows since the previous summary row (a subtotal) or of all data rows above (a running
    /// total) is a summary row and is left out. <see cref="UnitPriceSheet.TotalsRow"/> is the last summary row
    /// equal to the sum of all data rows. A candidate with prices that matches neither stays a data row, so the
    /// matcher blocks on it. Keyed rows are always data.
    /// </summary>
    private static (List<UnitPriceSheetRow> Rows, UnitPriceSheetRow? Totals) SplitSummaryRows(
        List<UnitPriceSheetRow> parsed)
    {
        var data = new List<UnitPriceSheetRow>();
        var summaries = new List<UnitPriceSheetRow>();
        decimal sumAll = 0m, sumSincePrevious = 0m;

        foreach (var row in parsed)
        {
            if (!IsSummaryCandidate(row))
            {
                data.Add(row);
                sumAll += row.AppraisalValue ?? 0m;
                sumSincePrevious += row.AppraisalValue ?? 0m;
                continue;
            }

            if (!row.HasPrice) continue;

            if (row.AppraisalValue is { } v)
            {
                if (Math.Abs(v - sumAll) <= 1m || Math.Abs(v - sumSincePrevious) <= 1m)
                {
                    summaries.Add(row);
                    sumSincePrevious = 0m;
                    continue;
                }
            }

            data.Add(row);   // priced, no key, adds up to nothing: the matcher reports it
        }

        // The sheet's grand total is the last summary row that equals ALL the data rows; a subtotal that is not
        // the whole sum (several towers, no grand total) is no sheet total.
        var totals = summaries.LastOrDefault(r => Math.Abs(r.AppraisalValue!.Value - sumAll) <= 1m);
        return (data, totals);
    }

    private static bool IsSummaryCandidate(UnitPriceSheetRow row) =>
        row.DisplayKey is null
        || new[] { row.Key1, row.Key2 }.Any(k => k is not null && SummaryLabels.Any(l => k.Contains(l, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Trimmed key text; blank and dash-only ("-", en dash, em dash) mean no key.</summary>
    public static string? CleanKey(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) || DashOnly.Contains(text) ? null : text;
    }

    private static string? TextOrNull(IXLCell cell)
    {
        var text = cell.GetString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static decimal? ReadPrice(
        IXLWorksheet ws, int row, int? col, string? header, string? key,
        List<UnitPriceImportError> errors, out bool hasContent)
    {
        hasContent = false;
        if (col is null) return null;

        var cell = ws.Cell(row, col.Value);
        var text = TextOrNull(cell);
        // A lone dash is how bank sheets write "nothing here".
        hasContent = text is not null && !DashOnly.Contains(text);
        if (!hasContent) return null;

        if (TryReadNumber(cell, out var value))
        {
            if (value >= 0) return value;

            errors.Add(new(row, key, UnitPriceImportErrorCodes.InvalidNumber,
                $"'{header}' must not be negative: '{cell.GetString().Trim()}'."));
            return null;
        }

        errors.Add(new(row, key, UnitPriceImportErrorCodes.InvalidNumber,
            $"'{header}' is not a valid number: '{cell.GetString().Trim()}'."));
        return null;
    }

    private static SheetCell ReadAttribute(IXLCell cell)
    {
        var text = TextOrNull(cell);
        return new SheetCell(text, text is not null && TryReadNumber(cell, out var n) ? n : null);
    }

    /// <summary>Numeric cell, or text such as "1,234.50". False for anything else (including formula errors).</summary>
    private static bool TryReadNumber(IXLCell cell, out decimal? value)
    {
        value = null;
        var v = cell.Value;
        if (v.IsNumber)
        {
            var n = v.GetNumber();
            if (Math.Abs(n) >= (double)MaxNumber) return false;
            value = Round((decimal)n);
            return true;
        }

        if (!v.IsText) return false;

        var text = v.GetText().Replace(",", "").Replace(" ", "").Trim();
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return false;
        if (Math.Abs(d) >= MaxNumber) return false;
        value = Round(d);
        return true;
    }

    public static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}
