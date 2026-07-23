using NSubstitute;
using Notification.Infrastructure.Email.Templates;
using Shared.Time;

namespace Notification.Tests.Email;

/// <summary>
/// Guards the three business-notification templates added for the missing RM emails:
/// quotation fee notice (with fee table), document follow-up, and route-back. Pins the Thai
/// subject/greeting, the table cells, and that dynamic text is HTML-encoded (no injection).
/// </summary>
public class NotificationEmailTemplatesTests
{
    private static EmailTemplateRenderer NewRenderer()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(new DateTime(2026, 6, 12));
        return new EmailTemplateRenderer(clock);
    }

    [Fact]
    public void QuotationFeeNotice_RendersGreeting_Columns_AndRowTotals()
    {
        var model = new QuotationFeeNoticeModel(
            RmName: "สมชาย ใจดี",
            CustomerName: "บริษัท ทดสอบ จำกัด",
            Columns:
            [
                new QuotationFeeNoticeColumn("69A00317", "LB", "จังหวัดภูเก็ต"),
                new QuotationFeeNoticeColumn("69A00323", "LB", "จังหวัดเชียงใหม่"),
            ],
            Rows:
            [
                new QuotationFeeNoticeRow("บริษัท เอ", ["25,680", "23,540"], "139,100"),
                new QuotationFeeNoticeRow("บริษัท บี", ["37,450", "26,750"], "169,060"),
            ],
            AdminName: "แอดมิน หนึ่ง");

        var html = NewRenderer().QuotationFeeNotice("แจ้งค่าธรรมเนียมประเมิน ลูกค้าราย บริษัท ทดสอบ จำกัด", model);

        Assert.Contains("เรียน สมชาย ใจดี", html);
        Assert.Contains("บริษัทประเมิน", html);
        Assert.Contains("69A00317", html);
        Assert.Contains("69A00323", html);
        Assert.Contains("LB", html);
        Assert.Contains("จังหวัดภูเก็ต", html);
        Assert.Contains("จังหวัดเชียงใหม่", html);
        Assert.Contains("25,680", html);
        Assert.Contains("139,100", html);
        Assert.Contains("แอดมิน หนึ่ง", html);
        Assert.Contains("รบกวนเลือกภายใน 2 วัน", html);
        Assert.Contains("แจ้งค่าธรรมเนียมประเมิน ลูกค้าราย <strong>บริษัท ทดสอบ จำกัด</strong>", html);
        // The visible subject-title heading is suppressed (subject stays as Subject line + preview).
        Assert.DoesNotContain("font-size:20px;font-weight:700;color:#222", html);
    }

    [Fact]
    public void QuotationFeeNotice_EncodesCompanyName_NoInjection()
    {
        var model = new QuotationFeeNoticeModel(
            RmName: "RM",
            CustomerName: "Cust",
            Columns: [new QuotationFeeNoticeColumn("A", null, null)],
            Rows: [new QuotationFeeNoticeRow("<script>x</script>", ["1"], "1")],
            AdminName: "Admin");

        var html = NewRenderer().QuotationFeeNotice("Subject", model);

        Assert.Contains("&lt;script&gt;x&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>x</script>", html);
    }

    [Fact]
    public void DocumentFollowupNotice_RendersRemarkAndSignature()
    {
        var model = new DocumentFollowupNoticeModel(
            RmName: "สมหญิง",
            CustomerName: "ลูกค้า ก",
            AppraisalNumber: "69A00317",
            Items:
            [
                new DocumentFollowupNoticeItem("เล่มประเมินสมบูรณ์", "รายละเอียด บรรทัดแรก\nบรรทัดสอง"),
                new DocumentFollowupNoticeItem("เอกสารแผนที่ภาพถ่ายทางอากาศ", null),
            ],
            AdminName: "แอดมิน สอง");

        var html = NewRenderer().DocumentFollowupNotice("งานติดตามเอกสารของลูกค้าราย ลูกค้า ก", model);

        Assert.Contains("เรียน สมหญิง", html);
        Assert.Contains("69A00317", html);
        // Document name as a bold header, notes below (long/multiline notes wrap).
        Assert.Contains("<strong>", html); // customer/appraisal
        Assert.Contains("1. เล่มประเมินสมบูรณ์", html);
        Assert.Contains("2. เอกสารแผนที่ภาพถ่ายทางอากาศ", html);
        Assert.Contains("รายละเอียด บรรทัดแรก<br/>บรรทัดสอง", html);
        Assert.Contains("แอดมิน สอง", html);
    }

    [Fact]
    public void RouteBackNotice_RendersRemarkAndBestRegards()
    {
        var model = new RouteBackNoticeModel(
            RmName: "สมชาย",
            Remark: "โฉนดไม่ตรงกับเอกสาร",
            SenderName: "แอดมิน สาม",
            SenderPhone: "02-123-4567");

        var html = NewRenderer().RouteBackNotice("ตรวจสอบและแก้ไขข้อมูลหลักประกันลูกค้า", model);

        Assert.Contains("เรียน สมชาย", html);
        Assert.Contains("โฉนดไม่ตรงกับเอกสาร", html);
        // Footer contact = sender full name + phone; no descriptive header sentence.
        Assert.Contains("กรุณาติดต่อ แอดมิน สาม 02-123-4567", html);
        Assert.DoesNotContain("เนื่องจากไม่ตรงกับเอกสารที่แนบมา", html);
        Assert.Contains("Best Regards", html);
    }

    [Fact]
    public void RemarkBlock_EncodesHtml_NoInjection()
    {
        var model = new RouteBackNoticeModel("RM", "<b>bad</b>", "Admin", null);

        var html = NewRenderer().RouteBackNotice("Subject", model);

        Assert.Contains("&lt;b&gt;bad&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>bad</b>", html);
    }
}
