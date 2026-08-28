using ClosedXML.Excel;

namespace Request.Application.Features.RequestTitles.ImportTitles.Reading;

/// <summary>
/// Reads an .xlsx into <see cref="RawSheet"/>s. Every worksheet is returned; deciding which ones are
/// recognised is the caller's job, so an unknown sheet can be reported rather than silently dropped.
/// </summary>
internal static class XlsxSheetReader
{
    internal static List<RawSheet> Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new BadRequestException(
                $"Could not read the Excel file. Check that it is an undamaged .xlsx file. ({ex.Message})");
        }

        using (workbook)
        {
            var sheets = new List<RawSheet>();

            foreach (var ws in workbook.Worksheets)
            {
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                if (lastRow < 1 || lastCol < 1) continue;

                var headers = new List<string>(lastCol);
                for (var c = 1; c <= lastCol; c++)
                    headers.Add(ws.Cell(1, c).GetString().Trim());

                // Stop well short of reading a sheet that cannot possibly be accepted. An .xlsx
                // compresses roughly 15×, so a file inside the 5 MB limit can still carry tens of
                // thousands of rows — materialising 41 columns of those before the row cap rejects
                // them is millions of strings built only to be thrown away.
                var totalDataRows = Math.Max(0, lastRow - 1);
                var readUpTo = Math.Min(lastRow, 1 + TitleImportLimits.MaxRows + 1);

                var rows = new List<IReadOnlyList<string>>();
                for (var r = 2; r <= readUpTo; r++)
                {
                    var cells = new List<string>(lastCol);
                    for (var c = 1; c <= lastCol; c++)
                        cells.Add(ReadCell(ws.Cell(r, c)));
                    rows.Add(cells);
                }

                sheets.Add(new RawSheet(ws.Name, headers, rows, totalDataRows));
            }

            return sheets;
        }
    }

    /// <summary>
    /// Cell text, normalised so a value typed as a number reads the same as one typed as text.
    /// Excel stores "12" in a General cell as the double 12, whose default string form is "12" —
    /// but a deed number entered as 00123 arrives as 123, which is why the template formats those
    /// columns as Text. Dates are rendered ISO so they never depend on the server's locale.
    /// </summary>
    private static string ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;

        return cell.DataType switch
        {
            XLDataType.Number => FormatNumber(cell.GetDouble()),
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd"),
            XLDataType.Boolean => cell.GetBoolean() ? "TRUE" : "FALSE",
            _ => cell.GetString().Trim()
        };
    }

    /// <summary>
    /// Renders a numeric cell the way the user typed it: 12 stays "12", 12.5 stays "12.5".
    ///
    /// The custom format does both jobs — it drops trailing zeros, and unlike the general formats it
    /// never falls back to scientific notation — so there is no need to special-case whole numbers
    /// through a long, which is what previously forced a floating-point equality test.
    /// </summary>
    private static string FormatNumber(double value)
        => value.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture);
}
