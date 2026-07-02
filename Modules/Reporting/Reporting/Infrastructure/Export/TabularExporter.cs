using System.Globalization;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using Reporting.Application.Services;
using Reporting.Contracts;
using Shared.Time;

namespace Reporting.Infrastructure.Export;

/// <summary>
/// The Reporting module's implementation of <see cref="ITabularExporter"/>. Owns the heavy
/// rendering engines: ClosedXML for Excel/CSV and the Puppeteer-backed <see cref="IPdfRenderer"/>
/// for PDF (an HTML table is built here, then rasterised by the shared browser pool).
/// </summary>
internal sealed class TabularExporter(
    IPdfRenderer pdfRenderer,
    IDateTimeProvider dateTimeProvider) : ITabularExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<ReportFile> ExportAsync<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<ReportColumn<T>> columns,
        string baseName,
        ReportFormat format,
        string? title = null,
        IReadOnlyList<FilterCriterion>? appliedFilters = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = dateTimeProvider.ApplicationNow.ToString("yyyyMMdd-HHmmss", Inv);

        return format switch
        {
            ReportFormat.Csv => new ReportFile(
                BuildCsv(rows, columns, appliedFilters),
                "text/csv",
                $"{baseName}-{timestamp}.csv"),

            ReportFormat.Pdf => new ReportFile(
                await pdfRenderer.RenderAsync(BuildHtml(rows, columns, title, appliedFilters), cancellationToken),
                "application/pdf",
                $"{baseName}-{timestamp}.pdf"),

            _ => new ReportFile(
                BuildExcel(rows, columns, title, appliedFilters),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{baseName}-{timestamp}.xlsx"),
        };
    }

    // "Label: Value" lines for the applied-filter block; a single "(none)" line when nothing was set.
    private static IReadOnlyList<string> FilterLines(IReadOnlyList<FilterCriterion>? appliedFilters)
    {
        if (appliedFilters is null) return [];
        if (appliedFilters.Count == 0) return ["(none)"];
        return appliedFilters.Select(f => $"{f.Label}: {f.Value}").ToList();
    }

    // ---------- Excel ----------

    private static byte[] BuildExcel<T>(
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns, string? title,
        IReadOnlyList<FilterCriterion>? appliedFilters)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");
        var span = Math.Max(1, columns.Count);

        var headerRow = 1;
        if (!string.IsNullOrWhiteSpace(title))
        {
            ws.Cell(headerRow, 1).Value = title;
            ws.Range(headerRow, 1, headerRow, span).Merge();
            ws.Row(headerRow).Style.Font.Bold = true;
            ws.Row(headerRow).Style.Font.FontSize = 13;
            headerRow++;
        }

        var filterLines = FilterLines(appliedFilters);
        if (filterLines.Count > 0)
        {
            ws.Cell(headerRow, 1).Value = "Applied filters";
            ws.Range(headerRow, 1, headerRow, span).Merge();
            ws.Row(headerRow).Style.Font.Bold = true;
            headerRow++;

            foreach (var line in filterLines)
            {
                ws.Cell(headerRow, 1).Value = line;
                ws.Range(headerRow, 1, headerRow, span).Merge();
                ws.Row(headerRow).Style.Font.Italic = true;
                ws.Row(headerRow).Style.Font.FontColor = XLColor.Gray;
                headerRow++;
            }

            headerRow++; // blank spacer row before the table header
        }

        for (var c = 0; c < columns.Count; c++)
            ws.Cell(headerRow, c + 1).Value = columns[c].Header;

        var header = ws.Row(headerRow);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        var r = headerRow + 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
                SetExcelCell(ws.Cell(r, c + 1), columns[c].Value(row), columns[c].Format);
            r++;
        }

        if (columns.Any(col => col.Total) && rows.Count > 0)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                var cell = ws.Cell(r, c + 1);
                if (col.Total && IsNumeric(col.Format))
                    SetExcelCell(cell, rows.Sum(row => ToDouble(col.Value(row)) ?? 0), col.Format);
                else if (c == 0)
                    cell.Value = $"Total ({rows.Count})";
            }

            ws.Row(r).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetExcelCell(IXLCell cell, object? value, ColumnFormat format)
    {
        if (value is null) return;

        switch (format)
        {
            case ColumnFormat.Integer when ToDouble(value) is { } d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case ColumnFormat.Decimal or ColumnFormat.Money when ToDouble(value) is { } d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ColumnFormat.Percent when ToDouble(value) is { } d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00\"%\"";
                break;
            case ColumnFormat.Date when ToDateTime(value) is { } dt:
                cell.Value = dt;
                cell.Style.NumberFormat.Format = "yyyy-mm-dd";
                break;
            case ColumnFormat.DateTime when ToDateTime(value) is { } dt:
                cell.Value = dt;
                cell.Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            default:
                cell.Value = Convert.ToString(value, Inv) ?? "";
                break;
        }
    }

    // ---------- CSV ----------

    private static byte[] BuildCsv<T>(
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns,
        IReadOnlyList<FilterCriterion>? appliedFilters)
    {
        var sb = new StringBuilder();

        // Applied-filter block as leading '#' comment lines, kept out of the data grid.
        var filterLines = FilterLines(appliedFilters);
        if (filterLines.Count > 0)
        {
            sb.AppendLine("# Applied filters");
            foreach (var line in filterLines)
                sb.AppendLine("# " + CsvCell(line));
            sb.AppendLine();
        }

        sb.AppendLine(string.Join(',', columns.Select(c => CsvCell(c.Header))));

        foreach (var row in rows)
            sb.AppendLine(string.Join(',', columns.Select(c => CsvCell(FormatText(c.Value(row), c.Format)))));

        // UTF-8 BOM so Excel opens Thai text with the right encoding.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string CsvCell(string value)
    {
        // Formula-injection guard: prefix a single quote when the first NON-space char is dangerous
        // (Excel still evaluates "  =cmd" as a formula, so test past any leading whitespace).
        var firstNonSpace = value.AsSpan().TrimStart();
        if (firstNonSpace.Length > 0 && firstNonSpace[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            value = "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    // ---------- PDF (HTML table → Puppeteer) ----------

    private static string BuildHtml<T>(
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns, string? title,
        IReadOnlyList<FilterCriterion>? appliedFilters)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html><html><head><meta charset="utf-8"><style>
            body{font-family:'Sarabun','Noto Sans Thai',Arial,sans-serif;font-size:11px;color:#111}
            h1{font-size:15px;margin:0 0 6px}
            .filters{font-size:10px;color:#555;margin:0 0 10px}
            .filters strong{display:block;color:#111;margin-bottom:2px}
            table{border-collapse:collapse;width:100%}
            th,td{border:1px solid #999;padding:3px 5px;text-align:left;vertical-align:top}
            th{background:#e9e9e9;font-weight:bold}
            tr:nth-child(even) td{background:#f6f6f6}
            td.num{text-align:right}
            tfoot td{font-weight:bold;background:#e9e9e9}
            </style></head><body>
            """);

        if (!string.IsNullOrWhiteSpace(title))
            sb.Append("<h1>").Append(HtmlEnc(title)).Append("</h1>");

        var filterLines = FilterLines(appliedFilters);
        if (filterLines.Count > 0)
        {
            sb.Append("<div class=\"filters\"><strong>Applied filters</strong>");
            foreach (var line in filterLines)
                sb.Append("<div>").Append(HtmlEnc(line)).Append("</div>");
            sb.Append("</div>");
        }

        sb.Append("<table><thead><tr>");
        foreach (var col in columns)
            sb.Append("<th>").Append(HtmlEnc(col.Header)).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            foreach (var col in columns)
            {
                var cls = IsNumeric(col.Format) ? " class=\"num\"" : "";
                sb.Append("<td").Append(cls).Append('>')
                    .Append(HtmlEnc(FormatText(col.Value(row), col.Format)))
                    .Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");

        if (columns.Any(col => col.Total) && rows.Count > 0)
        {
            sb.Append("<tfoot><tr>");
            for (var c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                if (col.Total && IsNumeric(col.Format))
                    sb.Append("<td class=\"num\">")
                        .Append(HtmlEnc(FormatText(rows.Sum(row => ToDouble(col.Value(row)) ?? 0), col.Format)))
                        .Append("</td>");
                else if (c == 0)
                    sb.Append("<td>").Append(HtmlEnc($"Total ({rows.Count})")).Append("</td>");
                else
                    sb.Append("<td></td>");
            }
            sb.Append("</tr></tfoot>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static string HtmlEnc(string value) => WebUtility.HtmlEncode(value);

    // ---------- Shared formatting helpers ----------

    private static bool IsNumeric(ColumnFormat f) =>
        f is ColumnFormat.Integer or ColumnFormat.Decimal or ColumnFormat.Money or ColumnFormat.Percent;

    private static string FormatText(object? value, ColumnFormat format)
    {
        if (value is null) return "";

        return format switch
        {
            ColumnFormat.Integer when ToDouble(value) is { } d => d.ToString("#,##0", Inv),
            ColumnFormat.Decimal or ColumnFormat.Money when ToDouble(value) is { } d => d.ToString("#,##0.00", Inv),
            ColumnFormat.Percent when ToDouble(value) is { } d => d.ToString("#,##0.00", Inv) + "%",
            ColumnFormat.Date when ToDateTime(value) is { } dt => dt.ToString("yyyy-MM-dd", Inv),
            ColumnFormat.DateTime when ToDateTime(value) is { } dt => dt.ToString("yyyy-MM-dd HH:mm", Inv),
            _ => Convert.ToString(value, Inv) ?? "",
        };
    }

    private static double? ToDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        decimal m => (double)m,
        int i => i,
        long l => l,
        float f => f,
        short s => s,
        byte b => b,
        _ => double.TryParse(Convert.ToString(value, Inv), NumberStyles.Any, Inv, out var r) ? r : null,
    };

    private static DateTime? ToDateTime(object? value) => value switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto.DateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        _ => DateTime.TryParse(Convert.ToString(value, Inv), Inv, DateTimeStyles.None, out var r) ? r : null,
    };
}
