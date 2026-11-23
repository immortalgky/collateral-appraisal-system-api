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
        IReadOnlyList<ReportSignoff>? signoffs = null,
        PdfLayout? pdfLayout = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = dateTimeProvider.ApplicationNow.ToString("yyyyMMdd-HHmmss", Inv);

        return format switch
        {
            ReportFormat.Csv => new ReportFile(
                BuildCsv(rows, columns, appliedFilters, signoffs),
                "text/csv",
                $"{baseName}-{timestamp}.csv"),

            ReportFormat.Pdf => new ReportFile(
                await pdfRenderer.RenderAsync(BuildHtml(rows, columns, title, appliedFilters, signoffs, pdfLayout), cancellationToken),
                "application/pdf",
                $"{baseName}-{timestamp}.pdf"),

            _ => new ReportFile(
                BuildExcel(rows, columns, ReportCode(baseName), title, appliedFilters, signoffs),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{baseName}-{timestamp}.xlsx"),
        };
    }

    // The report code that heads the Excel sheet, taken from the file-name stem:
    // "RCAS003-MonthlyWorkload" -> "RCAS003". Stems without a dash are used as-is.
    private static string ReportCode(string baseName)
    {
        var dash = baseName.IndexOf('-');
        return dash > 0 ? baseName[..dash] : baseName;
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
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns, string reportCode, string? title,
        IReadOnlyList<FilterCriterion>? appliedFilters, IReadOnlyList<ReportSignoff>? signoffs)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");
        var span = Math.Max(1, columns.Count);

        var headerRow = 1;
        if (!string.IsNullOrWhiteSpace(reportCode))
        {
            ws.Cell(headerRow, 1).Value = reportCode;
            ws.Range(headerRow, 1, headerRow, span).Merge();
            ws.Row(headerRow).Style.Font.Bold = true;
            ws.Row(headerRow).Style.Font.FontSize = 13;
            headerRow++;
        }

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
            // The criteria lines are self-describing ("Status: Approved"), so no heading above them.
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
            {
                SetExcelCell(ws.Cell(r, c + 1), columns[c].Value(row), columns[c].Format);
                if (SafeColor(columns[c].FontColor) is { } color) ws.Cell(r, c + 1).Style.Font.FontColor = XLColor.FromHtml(color);
            }
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
            r++;
        }

        // Top-align so a tall (wrapped) row doesn't bottom-align its other cells.
        ws.Range(headerRow, 1, r - 1, span).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        for (var c = 0; c < columns.Count; c++)
            if (columns[c].Center) ws.Range(headerRow, c + 1, r - 1, c + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Sign-off footer, below the table with a spacer row. A null value leaves an underlined
        // blank cell to be signed by hand.
        if (signoffs is { Count: > 0 })
        {
            r++;
            foreach (var s in signoffs)
            {
                ws.Cell(r, 1).Value = $"{s.Label}:";
                ws.Cell(r, 1).Style.Font.Bold = true;

                if (string.IsNullOrWhiteSpace(s.Value))
                    ws.Range(r, 2, r, Math.Min(3, Math.Max(2, span)))
                        .Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                else
                    ws.Cell(r, 2).Value = s.Value;

                r++;
            }
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
                var text = Convert.ToString(value, Inv) ?? "";
                cell.Value = text.Length > 32767 ? text[..32767] : text; // Excel's per-cell limit; a long nvarchar(max) must not 500 the export
                break;
        }
    }

    // ---------- CSV ----------

    private static byte[] BuildCsv<T>(
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns,
        IReadOnlyList<FilterCriterion>? appliedFilters, IReadOnlyList<ReportSignoff>? signoffs)
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

        // Sign-off footer as trailing '#' comment lines, matching the applied-filter block above and
        // keeping the data grid itself machine-readable.
        if (signoffs is { Count: > 0 })
        {
            sb.AppendLine();
            foreach (var s in signoffs)
                sb.AppendLine("# " + CsvCell($"{s.Label}: {s.Value}".TrimEnd()));
        }

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

    // A4 in CSS px (96 dpi) minus the renderer's 15mm margins on each side.
    private const double PxPerMm = 96 / 25.4;
    private const double PrintableLandscapePx = (297 - 30) * PxPerMm;
    private const double PrintablePortraitPx = (210 - 30) * PxPerMm;

    // ponytail: widths are estimates (chars x average glyph width), not measured text. An under-estimate
    // wraps a Text cell (fixed layout + overflow-wrap) or lets a nowrap cell overflow slightly; it never clips. Measure in the browser if it matters.
    private const int NaturalMaxChars = 32; // longer text wraps instead of widening its column
    private const double CellPaddingPx = 12; // cell padding (5+5) + borders, rounded
    private const int TextFloorChars = 3; // a Text column is never squeezed below this many chars
    // ponytail: Thai has no spaces, so a whole sentence is one "token" here although the browser breaks it by
    // dictionary. A Text cell token, or a Thai header word, counts at most this many chars toward min width; a
    // long Latin cell word may then break mid-word (Latin header words, up to ~29 chars while the mins fit, and nowrap values do not).
    private const int TextTokenCapChars = 16;
    private static readonly char[] Spaces = [' ', '\t', '\n', '\r']; // not NBSP / U+202F: those glue words together

    // Display length: combining marks (Thai vowels above/below, tone marks) take no horizontal space.
    private static int DisplayLength(string text) =>
        text.Count(ch => CharUnicodeInfo.GetUnicodeCategory(ch) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark));

    private static int LongestToken(string text) =>
        text.Split(Spaces, StringSplitOptions.RemoveEmptyEntries).Select(DisplayLength).DefaultIfEmpty(0).Max();

    // What a column must hold: longest text and longest unbreakable token (numbers, dates, ids are one
    // token), in display chars. Font-independent, so Fit can retry smaller fonts without re-scanning rows.
    // Nowrap = a non-Text or Repeat (identifier) column: td is white-space:nowrap, so its min is its whole value.
    private readonly record struct TextStat(int Longest, int Token, bool Nowrap = false, int Cell = 0, bool Ident = false)
    {
        // Cell = longest value itself (not header/label): what a Text column may "want" beyond the natural cap.
        public static TextStat Of(IEnumerable<string> texts) =>
            texts.Aggregate(default(TextStat), (m, t) =>
            {
                var len = DisplayLength(t);
                return new(Math.Max(m.Longest, len), Math.Max(m.Token, LongestToken(t)), false, Math.Max(m.Cell, len));
            });

        // A header (th) wraps at word boundaries, so it only needs its longest word, bold (1.1x). Only a Thai word
        // is capped at TextTokenCapChars (no spaces in Thai); a Latin word keeps its full length so it never breaks
        // mid-word. Never the whole header; not a cell value, so Cell stays 0.
        public static TextStat OfHeader(string header)
        {
            var word = header.Split(Spaces, StringSplitOptions.RemoveEmptyEntries).Select(w =>
            {
                var bold = (int)Math.Ceiling(DisplayLength(w) * 1.1);
                return w.Any(ch => ch is >= '\u0E00' and <= '\u0E7F') ? Math.Min(bold, TextTokenCapChars) : bold;
            }).DefaultIfEmpty(0).Max();
            return new(word, word);
        }

        // Text cell values: a long unbroken token (Thai sentence) counts at most TextTokenCapChars toward min.
        public TextStat CapToken() => this with { Token = Math.Min(Token, TextTokenCapChars) };

        // Headers (th) and the "Total (n)" label are bold, ~10% wider than regular text; not cell values.
        public TextStat Bold() => new((int)Math.Ceiling(Longest * 1.1), (int)Math.Ceiling(Token * 1.1), Nowrap);

        // Non-Text or Repeat (identifier) column: the longest value is one unbreakable token (header words may still wrap). The
        // bold totals value is widened 10% so a nowrap cell does not spill. Display chars.
        public static TextStat OfNowrap(IEnumerable<string> values, string? total, string header, bool ident)
        {
            var head = OfHeader(header);
            var token = Math.Max(Of(values).Longest, head.Token);
            if (total is not null) token = Math.Max(token, (int)Math.Ceiling(DisplayLength(total) * 1.1));
            return new(token, token, true, 0, ident);
        }

        public TextStat Max(TextStat o) =>
            new(Math.Max(Longest, o.Longest), Math.Max(Token, o.Token), Nowrap || o.Nowrap, Math.Max(Cell, o.Cell), Ident || o.Ident);

        // (min, natural) in px: natural = longest text capped at NaturalMaxChars (beyond that the cell wraps);
        // min = longest token, never above natural.
        public (double Min, double Natural, bool Nowrap, double Floor, double Want, bool Ident) Width(double fontSizePx)
        {
            var charW = fontSizePx * 0.6; // avg glyph (Latin + Thai)
            var natural = Math.Min(Longest, NaturalMaxChars) * charW + CellPaddingPx;
            var min = Token * charW + CellPaddingPx;
            // A nowrap column is never narrower than its value, even above NaturalMaxChars.
            if (Nowrap) natural = Math.Max(natural, min); else min = Math.Min(min, natural);
            // Want: only a Text column with content beyond the natural cap may take leftover page space.
            var want = Nowrap ? 0 : Math.Max(0, Cell - NaturalMaxChars) * charW;
            return (min, natural, Nowrap, Nowrap ? min : Math.Min(min, TextFloorChars * charW + CellPaddingPx), want, Ident);
        }
    }

    // Column widths for one table: everyone gets their min. Spare room then goes, in order, to: Text columns up
    // to their natural width; Text columns whose content is longer than the natural cap (up to that excess);
    // all Text columns in proportion to natural width, so the table reaches the page edge. Nowrap columns (numbers,
    // dates, identifiers) stay at their min, unless the set has no Text column: then numbers/dates (ids only if
    // nothing else) share the leftover so the table still fills the page.
    // Returns whether the table is in "nowrap mode".
    // If the mins overflow, only Text columns shrink, down to their floor.
    // ponytail: if the nowrap mins plus the Text floors still exceed the page, Text keeps its floor, the nowrap mins are scaled into the rest and
    // nowrap is dropped (numbers may then wrap rather than overlap their neighbour; everything is scaled
    // only if the floors alone do not fit); Split is the real fix for such reports.
    private static (double[] Widths, bool Nowrap) AllocateWidths(
        IReadOnlyList<(double Min, double Natural, bool Nowrap, double Floor, double Want, bool Ident)> cols, double printablePx)
    {
        var sumMin = cols.Sum(c => c.Min);
        var rigid = cols.Where(c => c.Nowrap).Sum(c => c.Min);
        var floors = cols.Where(c => !c.Nowrap).Sum(c => c.Floor);
        if (rigid + floors > printablePx)
            return (floors < printablePx
                ? cols.Select(c => c.Nowrap ? c.Min * (printablePx - floors) / rigid : c.Floor).ToArray() // Text keeps its floor
                : cols.Select(c => c.Min * printablePx / sumMin).ToArray(), false);

        if (sumMin >= printablePx)
        {
            var room = printablePx - rigid - floors;
            var excess = sumMin - rigid - floors; // sum of (Min - Floor) over Text columns
            return (cols.Select(c => c.Nowrap ? c.Min : c.Floor + (excess > 0 ? room * (c.Min - c.Floor) / excess : 0)).ToArray(), true);
        }

        // Spare room fills Text columns up to their natural width; nowrap columns stay at their min.
        var textSpare = cols.Where(c => !c.Nowrap).Sum(c => c.Natural - c.Min);
        var textExtra = Math.Min(printablePx - sumMin, textSpare);
        var widths = cols.Select(c => c.Nowrap || textSpare <= 0 ? c.Min : c.Min + textExtra * (c.Natural - c.Min) / textSpare).ToArray();
        var leftover = printablePx - widths.Sum();
        var want = cols.Sum(c => c.Want);
        if (leftover > 0 && want > 0)
        {
            var give = Math.Min(leftover, want);
            widths = widths.Select((w, i) => w + give * cols[i].Want / want).ToArray();
        }

        // Whatever is still left fills the page: Text columns first, in proportion to their natural width; with no
        // Text column, numbers/dates; with neither, the identifier columns.
        leftover = printablePx - widths.Sum();
        var textNatural = cols.Where(c => !c.Nowrap).Sum(c => c.Natural);
        if (leftover > 0 && textNatural > 0)
            widths = widths.Select((w, i) => cols[i].Nowrap ? w : w + leftover * cols[i].Natural / textNatural).ToArray();
        else if (leftover > 0)
        {
            // No Text column in the set: numbers/dates share it (identifiers only when nothing else is there).
            var grow = cols.Select(c => c.Nowrap && !c.Ident).ToArray();
            if (!grow.Contains(true)) grow = cols.Select(c => true).ToArray();
            var growNatural = cols.Where((c, i) => grow[i]).Sum(c => c.Natural);
            widths = widths.Select((w, i) => grow[i] ? w + leftover * cols[i].Natural / growNatural : w).ToArray();
        }

        return (widths, true);
    }

    internal static string BuildHtml<T>(
        IReadOnlyList<T> rows, IReadOnlyList<ReportColumn<T>> columns, string? title,
        IReadOnlyList<FilterCriterion>? appliedFilters, IReadOnlyList<ReportSignoff>? signoffs,
        PdfLayout? layout = null)
    {
        layout ??= PdfLayout.Default;
        var fs = Math.Clamp(layout.FontSizePx, PdfLayout.MinFontSizePx, PdfLayout.MaxFontSizePx);
        var printablePx = layout.Landscape ? PrintableLandscapePx : PrintablePortraitPx;

        // Cell text and column totals are formatted once; they feed both the width estimate and the table.
        var cells = rows.Select(row => columns.Select(col => FormatText(col.Value(row), col.Format)).ToArray()).ToList();
        var totals = columns
            .Select(col => rows.Count > 0 && col.Total && IsNumeric(col.Format)
                ? FormatText(rows.Sum(row => ToDouble(col.Value(row)) ?? 0), col.Format)
                : null)
            .ToArray();
        var stats = columns
            .Select((col, c) => IsNowrap(col)
                ? TextStat.OfNowrap(cells.Select(r => r[c]), totals[c], col.Header, col.Repeat)
                : TextStat.Of(cells.Select(r => r[c])).CapToken().Max(TextStat.OfHeader(col.Header)))
            .ToArray();
        var totalLabel = $"Total ({rows.Count})";
        var labelStat = TextStat.Of([totalLabel]).Bold();

        // The "Total (n)" label sits in a set's first cell unless that cell carries a numeric total.
        bool HasTotalsRow(List<int> set) => rows.Count > 0 && set.Any(c => columns[c].Total);
        bool HasLabel(List<int> set) => HasTotalsRow(set) && totals[set[0]] is null;
        List<TextStat> SetStats(List<int> set)
        {
            var list = set.Select(c => stats[c]).ToList();
            if (set.Count > 0 && HasLabel(set)) list[0] = list[0].Max(labelStat);
            return list;
        }

        var sets = layout.Overflow == PdfOverflow.Split
            ? PackColumnSets(columns, set => SetStats(set).Sum(z => z.Width(fs).Natural), printablePx)
            : [Enumerable.Range(0, columns.Count).ToList()];

        // Fit: the chosen font is a maximum. Shrink the table font (0.5px steps, floor 6px) until every
        // column's min width fits the page, so numbers are not broken mid-token. Split keeps the chosen font.
        var tableFs = (double)fs;
        if (layout.Overflow == PdfOverflow.Fit)
        {
            var all = SetStats(sets[0]);
            while (tableFs > PdfLayout.MinFontSizePx && all.Sum(z => z.Width(tableFs).Min) > printablePx)
                tableFs = Math.Max(PdfLayout.MinFontSizePx, tableFs - 0.5);
        }

        var sb = new StringBuilder();
        sb.Append($$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><style>
            @page{size:A4 {{(layout.Landscape ? "landscape" : "portrait")}};}
            body{margin:0;font-family:'Sarabun','Noto Sans Thai',Arial,sans-serif;font-size:{{fs}}px;color:#111}
            h1{font-size:1.36em;margin:0 0 6px}
            .filters{font-size:.9em;color:#555;margin:0 0 10px}
            .filters strong{display:block;color:#111;margin-bottom:2px}
            .setcap{font-size:.9em;color:#555;margin:0 0 4px}
            .set+.set{page-break-before:always}
            table{border-collapse:collapse;width:100%;table-layout:fixed;font-size:{{tableFs.ToString("0.##", Inv)}}px}
            th,td{border:1px solid #999;padding:3px 5px;text-align:left;vertical-align:top;overflow-wrap:anywhere}
            tr{page-break-inside:avoid}
            th{background:#e9e9e9;font-weight:bold}
            tr:nth-child(even) td{background:#f6f6f6}
            td.num{text-align:right}
            td.nw{white-space:nowrap}
            tr.total td{font-weight:bold;background:#e9e9e9}
            .signoff{margin-top:24px;page-break-inside:avoid}
            .signoff div{display:inline-block;margin-right:48px;vertical-align:bottom}
            .signoff strong{display:block;margin-bottom:4px}
            .sigline{border-bottom:1px solid #111;min-width:180px;height:18px}
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

        // "Down, then over": every row of column-set 1, then every row of set 2, ...
        for (var s = 0; s < sets.Count; s++)
        {
            var set = sets[s];
            var (widths, nowrap) = AllocateWidths(SetStats(set).Select(z => z.Width(tableFs)).ToList(), printablePx);
            var setTotal = widths.Sum();

            sb.Append("<div class=\"set\">");
            if (sets.Count > 1)
                sb.Append("<div class=\"setcap\">Columns ").Append(s + 1).Append(" of ").Append(sets.Count).Append("</div>");

            // Columns are % of the table. The widths normally add up to the page (100%); the clamp covers float noise.
            sb.Append("<table style=\"width:").Append(Math.Min(100, setTotal / printablePx * 100).ToString("0.##", Inv)).Append("%\"><colgroup>");
            foreach (var w in widths)
                sb.Append("<col style=\"width:").Append((w / setTotal * 100).ToString("0.##", Inv)).Append("%\">");
            sb.Append("</colgroup><thead><tr>");
            foreach (var c in set)
                sb.Append("<th").Append(CellStyle(columns[c], false)).Append('>').Append(HtmlEnc(columns[c].Header)).Append("</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var rowCells in cells)
            {
                sb.Append("<tr>");
                foreach (var c in set)
                {
                    var f = columns[c].Format;
                    var nw = nowrap && IsNowrap(columns[c]) ? " nw" : "";
                    var cls = IsNumeric(f) ? $" class=\"num{nw}\"" : nw.Length > 0 ? $" class=\"{nw.Trim()}\"" : "";
                    sb.Append("<td").Append(cls).Append(CellStyle(columns[c], true)).Append('>').Append(HtmlEnc(rowCells[c])).Append("</td>");
                }
                sb.Append("</tr>");
            }

            // Totals are the last body row, not a <tfoot>: Chromium repeats tfoot on every printed page.
            if (HasTotalsRow(set))
            {
                sb.Append("<tr class=\"total\">");
                for (var i = 0; i < set.Count; i++)
                {
                    if (totals[set[i]] is { } total)
                        sb.Append("<td class=\"num").Append(nowrap ? " nw" : "").Append('"').Append(CellStyle(columns[set[i]], false)).Append('>').Append(HtmlEnc(total)).Append("</td>");
                    else if (i == 0)
                        sb.Append("<td>").Append(HtmlEnc(totalLabel)).Append("</td>");
                    else
                        sb.Append("<td></td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");

            sb.Append("</table></div>");
        }

        if (signoffs is { Count: > 0 })
        {
            sb.Append("<div class=\"signoff\">");
            foreach (var s in signoffs)
            {
                sb.Append("<div><strong>").Append(HtmlEnc(s.Label)).Append(":</strong>");
                sb.Append("<div class=\"sigline\">").Append(HtmlEnc(s.Value ?? "")).Append("</div></div>");
            }
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    // Only a strict #rrggbb reaches the workbook / inline style; anything else is ignored.
    private static readonly System.Text.RegularExpressions.Regex HexColor = new(
        @"^#[0-9a-fA-F]{6}\z", System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static string? SafeColor(string? c) => c is not null && HexColor.IsMatch(c) ? c : null;

    // One inline style attribute per cell: optional font colour (data cells) + centering.
    private static string CellStyle<T>(ReportColumn<T> col, bool withColor)
    {
        var parts = new List<string>();
        if (withColor && SafeColor(col.FontColor) is { } color) parts.Add($"color:{color}");
        if (col.Center) parts.Add("text-align:center");
        return parts.Count == 0 ? "" : $" style=\"{string.Join(';', parts)}\"";
    }

    // Greedily packs columns (by index) into sets whose natural width (setWidth, which includes the totals
    // label) fits the printable width. Repeat columns are in every set (none marked = nothing repeats);
    // indices stay in original column order.
    private static List<List<int>> PackColumnSets<T>(
        IReadOnlyList<ReportColumn<T>> columns, Func<List<int>, double> setWidth, double printablePx)
    {
        var repeat = Enumerable.Range(0, columns.Count).Where(c => columns[c].Repeat).ToList();
        List<int> With(List<int> own, int? extra = null) => repeat.Concat(own).Concat(extra is { } e ? [e] : []).Order().ToList();

        var sets = new List<List<int>>();
        List<int> current = [];
        foreach (var c in Enumerable.Range(0, columns.Count).Except(repeat))
        {
            if (current.Count > 0 && setWidth(With(current, c)) > printablePx)
            {
                sets.Add(With(current));
                current = [];
            }
            current.Add(c);
        }
        sets.Add(With(current));

        return sets;
    }

    private static string HtmlEnc(string value) => WebUtility.HtmlEncode(value);

    // ---------- Shared formatting helpers ----------

    // Numbers, dates and identifier (Repeat) columns are never broken mid-value.
    private static bool IsNowrap<T>(ReportColumn<T> col) => col.Format != ColumnFormat.Text || col.Repeat;

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
