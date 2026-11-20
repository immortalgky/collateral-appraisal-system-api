using System.Text;
using System.Text.RegularExpressions;

namespace Reporting.Infrastructure.PdfAssembly;

/// <summary>
/// The running header and footer of the appraisal book, and the roman / arabic page labels.
///
/// Chromium cannot give a PDF per-page numbers that restart (I, II … then 1, 2 …), skip the bank's pasted pages, and
/// carry on through a merged attachment — its header/footer templates number each rendered document from 1 — so the
/// chrome is drawn here instead: one overlay page per numbered page, rendered by the same Chromium (so Thai shaping
/// and the Sarabun font match the body) and stamped onto the merged PDF by <see cref="PdfSharpAssembler"/>.
/// </summary>
internal static class BookChrome
{
    private static readonly Regex ReportNoDirective = new(
        @"book-report-no:\s*(?<no>[^\r\n]*?)\s*-->",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// The รายงานเลขที่ printed in every footer. A template-authored directive comment
    /// (<c>&lt;!-- book-report-no: … --&gt;</c>) rather than a model lookup, because the assembler only sees HTML. The
    /// value is already HTML-encoded by the renderer, so it is embedded as is.
    /// </summary>
    public static string ReadReportNumber(IEnumerable<string> htmls)
    {
        foreach (var html in htmls)
        {
            var m = ReportNoDirective.Match(html);
            if (m.Success)
                return m.Groups["no"].Value;
        }
        return string.Empty;
    }

    /// <summary>1 → "I", 4 → "IV" (front matter is a handful of pages).</summary>
    public static string Roman(int n)
    {
        string[] sym = ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];
        int[] val = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
        var sb = new StringBuilder();
        for (var i = 0; i < val.Length; i++)
            while (n >= val[i]) { sb.Append(sym[i]); n -= val[i]; }
        return sb.ToString();
    }

    /// <summary>One page of overlay: the label to print (null = print nothing, keep the slot) and its orientation.</summary>
    public readonly record struct OverlayPage(string Label, bool Landscape, string? Caption = null, bool NoHeader = false);

    /// <summary>
    /// The id of the internal book's appraisal-summary front part. The bank's form carries its own header bar (logo, title, report number, date),
    /// so on these pages the overlay draws the footer and page number only and leaves out its own logo and rule.
    /// </summary>
    public const string OwnHeaderPartId = "summary";

    /// <summary>Height of the title block stamped on attached-PDF pages, and where it sits (attached pages carry no running footer, so it sits close to the bottom edge).</summary>
    public const double TitleBlockHeightMm = 9, TitleBlockBottomMm = 9, SideMarginMm = 8;

    /// <summary>
    /// A standalone HTML document with one fixed-size block per overlay page: logo top-right over a rule, and the
    /// footer rule with รายงานเลขที่ on the left and "- n -" on the right. Margins are declared 0 through the
    /// renderer's directive comments, and the document never forces a break after its last block (that would add
    /// a blank trailing page and shift nothing, but is easy to avoid).
    /// </summary>
    public static string OverlayHtml(IReadOnlyList<OverlayPage> pages, string reportNumber)
    {
        var sb = new StringBuilder(2048 + pages.Count * 600);
        sb.Append("""
            <!DOCTYPE html><html lang="th"><head><meta charset="UTF-8"/>
            <!-- pdf-margin-x:0mm -->
            <!-- pdf-margin-y:0mm -->
            <style>
            @font-face { font-family:'Sarabun'; font-weight:400; src:url('Sarabun.ttf') format('truetype'); }
            @font-face { font-family:'Sarabun'; font-weight:700; src:url('Sarabun-Bold.ttf') format('truetype'); }
            @page { size:A4; margin:0; }
            @page land { size:A4 landscape; margin:0; }
            * { box-sizing:border-box; margin:0; padding:0; }
            html, body { background:none; font-family:'Sarabun','Noto Sans Thai','Thonburi','Leelawadee UI',sans-serif; color:#333; }
            .ov { position:relative; width:210mm; height:296mm; overflow:hidden; }
            .ov + .ov { break-before:page; }
            .ov.land { page:land; width:297mm; height:209mm; }
            .ov.land .hd, .ov.land .ft { left:10mm; right:10mm; }
            .ov .hd { position:absolute; left:15mm; right:15mm; top:12mm; height:10.5mm; border-bottom:0.3mm solid #1f2b4d;
                      display:flex; justify-content:flex-end; align-items:flex-start; }
            .ov .hd img { height:8mm; width:auto; display:block; }
            .ov .ft { position:absolute; left:15mm; right:15mm; bottom:12mm; height:7mm; border-top:0.3mm solid #1f2b4d;
                      display:flex; justify-content:space-between; align-items:flex-end; font-size:9pt; line-height:1.2; padding-bottom:1mm; }
            .ov .tbw { position:absolute; left:8mm; right:8mm; bottom:9mm; color:#000; }
            .tb { border-collapse:collapse; width:100%; table-layout:fixed; height:9mm; }
            .tb td { border:0.3mm solid #000; padding:0.8mm 2mm; vertical-align:middle; }
            .tb td.l { width:15%; text-align:center; font-size:9pt; line-height:1.2; }
            .tb td.n { width:26%; }
            .tb td.v { font-weight:700; font-size:11pt; line-height:1.25; }
            </style></head><body>
            """);
        foreach (var p in pages)
        {
            sb.Append("<div class=\"ov").Append(p.Landscape ? " land" : "").Append("\">");
            if (p.Label.Length > 0)
            {
                if (!p.NoHeader)
                    sb.Append("<div class=\"hd\"><img src=\"logo-lh-bank.svg\" alt=\"\"/></div>");
                sb.Append("<div class=\"ft\"><span>รายงานเลขที่ : ").Append(reportNumber).Append("</span><span>- ")
                  .Append(p.Label).Append(" -</span></div>");
            }
            if (p.Caption is not null)
                // the one-row title block (same layout as partials/book-title-block); text is already HTML-encoded
                sb.Append("<div class=\"tbw\"><table class=\"tb\"><tr>")
                  .Append("<td class=\"l\">เอกสาร</td><td class=\"v\">").Append(p.Caption).Append("</td>")
                  .Append("<td class=\"l\">รายงานเลขที่</td><td class=\"v n\">").Append(reportNumber).Append("</td></tr></table></div>");
            sb.Append("</div>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
