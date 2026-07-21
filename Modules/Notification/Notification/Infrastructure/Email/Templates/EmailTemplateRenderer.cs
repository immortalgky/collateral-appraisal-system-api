using Shared.Time;

namespace Notification.Infrastructure.Email.Templates;

/// <summary>
/// Renders branded HTML email bodies using C# string concatenation.
/// No external template engine — Thai-safe UTF-8, zero extra dependencies.
/// The admin-typed <c>Content</c> (if any) is wrapped inside the branded LH Bank shell:
/// orange header + logo, the signature multicolor "Choice Bar", and an auto-generated /
/// confidentiality footer. Layout is table-based with inline styles for Outlook/Gmail safety.
/// </summary>
internal sealed class EmailTemplateRenderer(IDateTimeProvider clock) : IEmailTemplateRenderer
{
    public string QuotationSent(string subject, string? adminContent) =>
        Wrap(subject, BuildBody(adminContent));

    public string MeetingInvitation(string subject, string? adminContent) =>
        Wrap(subject, BuildBody(adminContent));

    public string QuotationFeeNotice(string subject, QuotationFeeNoticeModel model)
    {
        var sb = new System.Text.StringBuilder();

        sb.Append("<p style=\"margin:0 0 12px;\">เรียน ").Append(Enc(model.RmName)).Append("</p>");

        sb.Append("<p style=\"margin:0 0 4px;\">แจ้งค่าธรรมเนียมประเมิน ลูกค้าราย <strong>")
            .Append(Enc(model.CustomerName ?? "-")).Append("</strong></p>");
        sb.Append("<p style=\"margin:0 0 12px;font-weight:700;color:#c0392b;\">(รบกวนเลือกภายใน 2 วัน)</p>");

        // ── Fee-comparison table ──────────────────────────────────────────────
        sb.Append("<table role=\"presentation\" cellpadding=\"6\" cellspacing=\"0\" " +
                  "style=\"border-collapse:collapse;width:100%;font-size:13px;border:1px solid #d0d3d7;\">");
        sb.Append("<thead><tr style=\"background:#f2f4f6;\">");
        sb.Append(Th("บริษัทประเมิน", "center"));
        foreach (var col in model.Columns)
            sb.Append(ColumnHeader(col));
        // Last header carries an intentional line break, so it is emitted as raw markup.
        sb.Append("<th style=\"border:1px solid #d0d3d7;text-align:center;font-weight:700;\">" +
                  "ค่าธรรมเนียม / บาท<br/>รวม Vat</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in model.Rows)
        {
            sb.Append("<tr>");
            sb.Append(Td(row.CompanyName, "left"));
            foreach (var cell in row.Cells)
                sb.Append(Td(cell, "right"));
            sb.Append("<td style=\"border:1px solid #d0d3d7;text-align:right;font-weight:700;color:#c0392b;\">")
                .Append(Enc(row.Total)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");

        // ── Note ──────────────────────────────────────────────────────────────
        sb.Append("<p style=\"margin:14px 0 0;font-size:13px;line-height:1.6;\">")
            .Append("<strong>หมายเหตุ</strong> ณ วันสำรวจหากพบว่ามีที่ดิน, สิ่งปลูกสร้างเพิ่มหรือไม่ตรงตามที่แจ้ง " +
                    "ค่าธรรมเนียมอาจมีเรียกเก็บเพิ่ม<br/>")
            .Append("- หากเลือกบริษัทประเมินราคาภายนอกแล้ว รบกวนสินเชื่อแจ้งชื่อกลับสำนักประเมินภายใน 2 วัน " +
                    "(นับจากวันแจ้งเลือกบริษัทประเมินฯ) (ตามมติที่ประชุม ลงวันที่ 31 ม.ค. 2560)<br/>")
            .Append("- หากเลยกำหนดตามที่แจ้ง ขอให้เป็นดุลยพินิจของทางสำนักประเมินในการตัดสินใจ เลือกบริษัทประเมิน หรือ ยกเลิก")
            .Append("</p>");

        sb.Append("<p style=\"margin:16px 0 0;\">จึงเรียนมาเพื่อโปรดทราบ</p>");
        sb.Append("<p style=\"margin:4px 0 0;\">").Append(Enc(model.AdminName)).Append("</p>");

        return Wrap(subject, sb.ToString(), showTitle: false);
    }

    public string DocumentFollowupNotice(string subject, DocumentFollowupNoticeModel model)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<p style=\"margin:0 0 12px;\">เรียน ").Append(Enc(model.RmName)).Append("</p>");
        sb.Append("<p style=\"margin:0 0 12px;\">งานติดตามเอกสารของลูกค้าราย <strong>")
            .Append(Enc(model.CustomerName ?? "-"))
            .Append("</strong> หมายเลขเล่มประเมิน <strong>")
            .Append(Enc(model.AppraisalNumber ?? "-")).Append("</strong></p>");

        // Each requested document: numbered name as a header, remark/notes wrapping below it.
        var itemNumber = 1;
        foreach (var item in model.Items)
        {
            sb.Append("<div style=\"margin:0 0 12px;\">");
            sb.Append("<p style=\"margin:0 0 2px;font-weight:700;\">")
                .Append(itemNumber).Append(". ").Append(Enc(item.DocumentName)).Append("</p>");
            if (!string.IsNullOrWhiteSpace(item.Notes))
                sb.Append("<p style=\"margin:0 0 0 18px;white-space:pre-wrap;color:#555;\">")
                    .Append(Enc(item.Notes).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br/>"))
                    .Append("</p>");
            sb.Append("</div>");
            itemNumber++;
        }

        sb.Append("<p style=\"margin:16px 0 0;\">จึงเรียนมาเพื่อโปรดทราบ</p>");
        sb.Append("<p style=\"margin:4px 0 0;\">").Append(Enc(model.AdminName)).Append("</p>");
        return Wrap(subject, sb.ToString(), showTitle: false);
    }

    public string RouteBackNotice(string subject, RouteBackNoticeModel model)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<p style=\"margin:0 0 12px;\">เรียน ").Append(Enc(model.RmName)).Append("</p>");

        // Body is just the sender's comment (no descriptive header).
        sb.Append(RemarkBlock(model.Remark));

        // Footer contact = sender's full name + phone.
        var contact = Enc(model.SenderName);
        if (!string.IsNullOrWhiteSpace(model.SenderPhone))
            contact += " " + Enc(model.SenderPhone);
        sb.Append("<p style=\"margin:16px 0 0;\">หากมีข้อสงสัยหรือต้องการสอบถามข้อมูลเพิ่มเติม กรุณาติดต่อ ")
            .Append(contact).Append("</p>");
        sb.Append("<p style=\"margin:12px 0 0;\">Best Regards</p>");
        return Wrap(subject, sb.ToString(), showTitle: false);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static string Enc(string text) => HtmlEncode(text);

    private static string Th(string text, string align) =>
        $"<th style=\"border:1px solid #d0d3d7;text-align:{align};font-weight:700;\">{Enc(text)}</th>";

    // Column header = report number, then property type + province name each on its own line.
    private static string ColumnHeader(QuotationFeeNoticeColumn col)
    {
        var lines = new List<string> { Enc(col.ReportNumber) };
        if (!string.IsNullOrWhiteSpace(col.PropertyType)) lines.Add(Enc(col.PropertyType));
        if (!string.IsNullOrWhiteSpace(col.Province)) lines.Add(Enc(col.Province));
        return "<th style=\"border:1px solid #d0d3d7;text-align:center;font-weight:700;\">"
               + string.Join("<br/>", lines) + "</th>";
    }

    private static string Td(string text, string align) =>
        $"<td style=\"border:1px solid #d0d3d7;text-align:{align};\">{Enc(text)}</td>";

    // Renders the free-text decision remark (line breaks preserved), or a neutral fallback.
    private static string RemarkBlock(string? remark) =>
        string.IsNullOrWhiteSpace(remark)
            ? string.Empty
            : "<p style=\"margin:0 0 12px;white-space:pre-wrap;\">"
              + Enc(remark).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br/>")
              + "</p>";

    // Newlines are converted to <br/> AFTER encoding (so our tags survive). white-space:pre-wrap
    // is kept for clients that honour it (preserves runs of spaces), but Outlook's Word engine
    // ignores it — the <br/> is what actually keeps the admin's line breaks there.
    private static string BuildBody(string? adminContent) =>
        string.IsNullOrWhiteSpace(adminContent)
            ? string.Empty
            : "<p style=\"margin:0;white-space:pre-wrap;\">"
              + HtmlEncode(adminContent)
                  .Replace("\r\n", "\n").Replace("\r", "\n")
                  .Replace("\n", "<br/>")
              + "</p>";

    private string Wrap(string subject, string bodyHtml, bool showTitle = false)
    {
        var encodedSubject = HtmlEncode(subject);
        var year = clock.ApplicationNow.Year;

        // The visible subject heading is suppressed by default — the subject is still the mail's
        // Subject line + hidden inbox-preview text, so repeating it in the body is redundant.
        var titleRow = showTitle
            ? "        <tr><td class=\"sarabun\" style=\"padding:24px 28px 4px;\"><div style=\"font-size:20px;font-weight:700;color:#222;line-height:1.35;\">" + encodedSubject + "</div></td></tr>\n"
            : string.Empty;
        var bodyPaddingTop = showTitle ? "12px" : "24px";

        return
            "<!DOCTYPE html>\n" +
            "<html lang=\"th\" xmlns:o=\"urn:schemas-microsoft-com:office:office\">\n" +
            "<head>\n" +
            "  <meta charset=\"UTF-8\"/>\n" +
            "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>\n" +
            "  <meta name=\"x-apple-disable-message-reformatting\"/>\n" +
            "  <title>" + encodedSubject + "</title>\n" +
            "  <!--[if mso]><style>* { font-family: Tahoma, sans-serif !important; }</style><![endif]-->\n" +
            "  <style>\n" +
            "    body { margin:0; padding:0; background:#eef0f2; }\n" +
            "    table { border-collapse:collapse; }\n" +
            "    img { border:0; outline:none; text-decoration:none; }\n" +
            "    .sarabun { font-family:'Sarabun', Tahoma, 'Segoe UI', sans-serif; }\n" +
            "    .choicebar td { height:5px; line-height:5px; font-size:0; }\n" +
            "  </style>\n" +
            "</head>\n" +
            "<body>\n" +
            // hidden inbox preview text
            "  <div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">" + encodedSubject + "</div>\n" +
            "  <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#eef0f2;\">\n" +
            "    <tr><td align=\"center\" style=\"padding:0;\">\n" +
            "      <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;max-width:100%;background:#ffffff;\">\n" +
            // ---- HEADER: off-white band + logo ----
            "        <tr><td style=\"background:#f6f6f4;padding:22px 28px;\">\n" +
            "          <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>\n" +
            // Logo: embedded LH Bank PNG (transparent) attached inline by SmtpEmailSender,
            // keyed by EmailBranding.LogoContentId. The font styling on the <img> is the
            // "styled alt" fallback: ignored when the image renders, but when a client blocks
            // the image (e.g. Outlook external-sender protection) the alt text shows as a
            // branded "LH Bank" wordmark instead of tiny default-styled placeholder text.
            "            <td valign=\"middle\"><img src=\"cid:" + EmailBranding.LogoContentId + "\" alt=\"LH Bank\" width=\"148\" height=\"38\" style=\"display:block;border:0;font-family:'Sarabun',Tahoma,'Segoe UI',sans-serif;font-size:22px;font-weight:700;color:#0080be;line-height:38px;\"/></td>\n" +
            "            <td valign=\"middle\" align=\"right\" class=\"sarabun\" style=\"color:#6d6e71;font-size:13px;font-weight:600;\">Collateral Appraisal System</td>\n" +
            "          </tr></table>\n" +
            "        </td></tr>\n" +
            // ---- CHOICE BAR ----
            // Choice Bar colours match the LH Bank logo's swatch (left to right).
            // Width + height are inline on every cell so the stripe stays full-width even when
            // an email client strips the <head> <style> block.
            "        <tr><td style=\"font-size:0;line-height:0;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;border-collapse:collapse;\"><tr>\n" +
            "          <td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#ced629;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#47b9c0;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#8b3f92;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#ed8068;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#0080be;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#f5bf0e;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#94988d;\">&nbsp;</td>" +
            "<td width=\"12.5%\" height=\"6\" style=\"width:12.5%;height:6px;line-height:6px;font-size:0;background:#f08d1d;\">&nbsp;</td>\n" +
            "        </tr></table></td></tr>\n" +
            // ---- SUBJECT / TITLE (optional) ----
            titleRow +
            // ---- BODY (admin content) ----
            "        <tr><td class=\"sarabun\" style=\"padding:" + bodyPaddingTop + " 28px 24px;color:#3a3a3a;font-size:14px;line-height:1.7;\">" + bodyHtml + "</td></tr>\n" +
            // ---- FOOTER ----
            "        <tr><td class=\"sarabun\" style=\"background:#f4f5f7;border-top:1px solid #e3e6ea;padding:18px 28px;\">\n" +
            "          <p style=\"margin:0;font-size:12px;line-height:1.6;color:#7a7d82;\">อีเมลฉบับนี้ถูกสร้างขึ้นโดยอัตโนมัติจากระบบ <strong>Collateral Appraisal System</strong> กรุณาอย่าตอบกลับอีเมลนี้<br/>\n" +
            "            <span style=\"color:#9aa0a6;\">This email was generated automatically by the Collateral Appraisal System. Please do not reply.</span></p>\n" +
            "          <p style=\"margin:10px 0 0;font-size:11px;line-height:1.6;color:#9aa0a6;\">ข้อมูลในอีเมลนี้เป็นความลับ สำหรับผู้รับที่ระบุไว้เท่านั้น หากท่านได้รับโดยมิได้ตั้งใจ โปรดลบอีเมลนี้และแจ้งผู้ส่ง · This message is confidential and intended solely for the addressee.</p>\n" +
            "          <p style=\"margin:10px 0 0;font-size:11px;color:#b0b4b8;\">© " + year + " LH Bank — Land and Houses Bank Public Company Limited</p>\n" +
            "        </td></tr>\n" +
            "      </table>\n" +
            "    </td></tr>\n" +
            "  </table>\n" +
            "</body>\n" +
            "</html>";
    }

    private static string HtmlEncode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);
}
