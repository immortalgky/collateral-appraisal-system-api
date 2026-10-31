using System.Globalization;
using System.Net;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// The bank's one default "ขอนำส่งงานประเมิน" email, built here only so a template store can replace it later.
/// A value the case does not know yet is null and is written as its placeholder, which the send fills in.
/// Case values are HTML-encoded: they come from the request, not from the admin.
/// </summary>
public static class AssignmentEmailTemplate
{
    public const string CompanyPlaceholder = AssignmentEmail.CompanyPlaceholder;
    public const string ResponsiblePlaceholder = AssignmentEmail.ResponsiblePlaceholder;

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

    public static string BodyHtml(Values v) =>
        $"<p>เรียน {Encode(v.Company) ?? CompanyPlaceholder}</p>" +
        "<p>ขอนำส่งงานประเมิน ลูกค้าจำนวน 1 รายการ (หากได้วันเข้าสำรวจแล้ว โปรดแจ้งกลับ)</p>" +
        $"<p>App No. : {Text(v.AppraisalNumber)}<br>" +
        $"ชื่อลูกค้า : {Text(v.CustomerName)}<br>" +
        $"ชื่อผู้ติดต่อ : {Text(v.ContactName)}<br>" +
        $"โทรศัพท์ : {Text(v.ContactPhone)}<br>" +
        $"ค่าธรรมเนียม : {v.FeeTotal?.ToString("N2", CultureInfo.InvariantCulture) ?? "-"}<br>" +
        $"การชำระค่าธรรมเนียม : {Text(v.FeePaymentText)}<br>" +
        $"ผู้รับผิดชอบ : {Encode(v.Responsible) ?? ResponsiblePlaceholder}<br>" +
        $"หมายเหตุ : {Text(v.Remark)}</p>" +
        "<p>จึงเรียนมาเพื่อทราบและดำเนินการต่อ</p>";

    private static string? Encode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlEncode(value.Trim());

    // A known-but-blank case field reads as a dash, as the bank's own emails do.
    private static string Text(string? value) => Encode(value) ?? "-";
}
