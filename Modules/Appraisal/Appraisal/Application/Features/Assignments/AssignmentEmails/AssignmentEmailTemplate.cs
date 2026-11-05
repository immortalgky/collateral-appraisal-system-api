using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// The bank's one default "ขอนำส่งงานประเมิน" email, built here only so a template store can replace it later.
/// A value the case does not know yet is null and is written as its placeholder, which the send fills in.
/// Case values are HTML-encoded: they come from the request, not from the admin.
/// </summary>
public static partial class AssignmentEmailTemplate
{
    [GeneratedRegex(" {2,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SpaceRun();

    public record Values(
        string? Company,
        string? CustomerName,
        string? AppraisalNumber,
        string? ContactName,
        string? ContactPhone,
        decimal? FeeTotal,
        string? FeePaymentText,
        string? Responsible,
        string? Remark);

    // One line, single-spaced and free of control characters, so the subject it writes always passes save
    // validation: a header cannot carry a line break, and a missing value leaves no double space.
    public static string Subject(Values v)
    {
        var raw = $"ขอนำส่งงานประเมิน ลูกค้าราย {v.CustomerName} {v.AppraisalNumber}";
        var printable = new string(raw.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return string.Join(' ', printable.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // One paragraph, lines joined with <br> and each blank line a <br><br>: an empty <p></p> collapses in mail clients
    // and in the editor, while a hard break survives both.
    public static string BodyHtml(Values v) =>
        $"<p>เรียน {Encode(v.Company) ?? AssignmentEmail.CompanyPlaceholder}<br><br>" +
        "ขอนำส่งงานประเมิน ลูกค้าจำนวน 1 รายการ (หากได้วันเข้าสำรวจแล้ว โปรดแจ้งกลับ)<br>" +
        $"หมายเลขประเมิน: {Text(v.AppraisalNumber)}<br>" +
        $"ชื่อลูกค้า: {Text(v.CustomerName)}<br>" +
        $"ชื่อผู้ติดต่อ: {Text(v.ContactName)}<br>" +
        $"โทรศัพท์: {Text(v.ContactPhone)}<br>" +
        $"ค่าธรรมเนียม: {v.FeeTotal?.ToString("N2", CultureInfo.InvariantCulture) ?? "-"}<br>" +
        $"การชำระค่าธรรมเนียม: {Text(v.FeePaymentText)}<br>" +
        $"ผู้รับผิดชอบ: {Encode(v.Responsible) ?? AssignmentEmail.ResponsiblePlaceholder}<br>" +
        $"หมายเหตุ: {Remark(v.Remark)}<br><br>" +
        "จึงเรียนมาเพื่อทราบและดำเนินการต่อ</p>";

    private static string? Encode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlEncode(value.Trim());

    // The admin's remark keeps its shape: lines joined with <br>, leading and inner spaces / tabs kept as &nbsp; (a tab is
    // four), only blank lines at the start and end dropped. Plain tags, no <pre> or CSS, so the editor keeps them.
    private static string Remark(string? value)
    {
        // Every kind of line break is one; other control characters and the invisible ones that hide text are dropped.
        // ZWJ / ZWNJ stay: emoji sequences and some scripts need them.
        var text = (value ?? "").Replace("\r\n", "\n");
        var kept = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\r' or 0x2028 or 0x2029 or 0x85 or '\v' or '\f') kept.Append('\n');
            else if (rune.Value is '\n' or '\t' || !(Rune.IsControl(rune) || IsInvisible(rune))) kept.Append(rune.ToString());
        }

        text = kept.ToString();
        var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
        var first = lines.FindIndex(l => l.Length > 0);
        if (first < 0) return "-";
        var last = lines.FindLastIndex(l => l.Length > 0);

        return string.Join("<br>", lines.Skip(first).Take(last - first + 1).Select(line =>
        {
            var body = line.TrimStart(' ', '\t');
            var indent = string.Concat(line[..^body.Length].Select(c => c == '\t' ? "&nbsp;&nbsp;&nbsp;&nbsp;" : "&nbsp;"));
            // Tabs and runs of spaces collapse when rendered: a tab is four &nbsp;, and all but the last space of a run
            // is one; a single space stays so the line still wraps.
            var inner = SpaceRun().Replace(WebUtility.HtmlEncode(body).Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;"),
                m => string.Concat(Enumerable.Repeat("&nbsp;", m.Length - 1)) + " ");
            return indent + inner;
        }));
    }

    // Format characters (zero-width space, BOM, bidi controls, tags, soft hyphen...) except the joiners emoji sequences and
    // some scripts need, plus the Hangul fillers, which draw nothing.
    private static bool IsInvisible(Rune r) =>
        r.Value is 0x115F or 0x1160 or 0x3164 or 0xFFA0
        || (r.Value is not (0x200C or 0x200D) && Rune.GetUnicodeCategory(r) == UnicodeCategory.Format);

    // A known-but-blank case field reads as a dash, as the bank's own emails do.
    private static string Text(string? value) => Encode(value) ?? "-";
}
