using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The external company letter ends at its own ruled value table: no customer phrase in the intro, no per-approach table, no certification, closing or signers.</summary>
public class BookLetterTrimTests
{
    private static Task<string> LetterAsync() =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-letter-external' }}", new AppraisalSummaryModel
        {
            IsExternal = true, CompanyName = "บริษัทตัวอย่าง จำกัด", CustomerName = "นายลูกค้า ตัวอย่าง", BankName = "ธนาคารตัวอย่าง", CollateralValue = 1_000_000m,
            ForcedSaleValue = 700_000m, SelectionReason = "เหตุผลตัวอย่าง",
            ApproachValues = [new ApproachValueRow { ApproachType = "Market", ApproachLabel = "วิธีเปรียบเทียบราคาตลาด", Value = 1_000_000m }]
        }, CancellationToken.None);

    [Fact]
    public async Task The_intro_has_no_customer_phrase_and_still_reads_cleanly()
    {
        var html = await LetterAsync();

        html.Should().Contain("ประเมินมูลค่าทรัพย์สินนั้น")
            .And.NotContain("ให้แก่").And.NotContain("ทรัพย์สิน นั้น").And.NotContain("ทรัพย์สิน\n    นั้น");
        html.Should().Contain("<th class=\"kv-label\" scope=\"row\">ลูกค้า</th>");   // the ลูกค้า row stays
    }

    [Fact]
    public async Task The_approach_table_and_the_selection_reason_are_gone()
    {
        var html = await LetterAsync();

        html.Should().NotContain("มูลค่าแต่ละแนวทางที่วิเคราะห์").And.NotContain("approach-table").And.NotContain("วิธีเปรียบเทียบราคาตลาด")
            .And.NotContain("เหตุผลการเลือกวิธีประเมิน").And.NotContain("เหตุผลตัวอย่าง");
    }

    [Fact]
    public async Task The_certification_closing_and_signature_block_are_gone_so_the_letter_ends_at_the_value_table()
    {
        var html = await LetterAsync();

        html.Should().NotContain("ขอรับรองว่า").And.NotContain("ปรากฏตามรายงานที่แนบมาพร้อมนี้").And.NotContain("จึงเรียนมาเพื่อโปรดพิจารณา")
            .And.NotContain("ขอแสดงความนับถือ").And.NotContain("sig-").And.NotContain("ผู้ตรวจสอบ").And.NotContain("ผู้ทบทวน").And.NotContain("กรรมการผู้มีอำนาจลงนาม");
        html.Should().Contain("มูลค่าทรัพย์สิน").And.Contain("มูลค่าบังคับขาย");
        html.TrimEnd().Should().EndWith("</table>\n\n</div>");
    }

    [Fact]
    public async Task The_letter_keeps_its_own_table_and_value_rows_and_does_not_borrow_the_summary_pages_markup()
    {
        var html = await LetterAsync();

        html.Should().Contain("<table class=\"kv-table\">").And.Contain("1,000,000.00 บาท").And.Contain("ขอเรียนแจ้งผลการประเมินมูลค่าทรัพย์สิน ดังนี้")
            .And.NotContain("bk-valbox").And.NotContain("bk-ext").And.NotContain("bk-title");
    }

    [Fact]
    public void The_assessment_method_line_names_the_approach_never_the_method()
    {
        // A Market approach prints "วิธีเปรียบเทียบราคาตลาด" whatever method (WQS / Sale Grid) stands behind it; repeats collapse.
        ExternalBookBuilder.SelectedApproachLine(["Market"]).Should().Be("วิธีเปรียบเทียบราคาตลาด");
        ExternalBookBuilder.SelectedApproachLine(["Market", "Market", "Cost"]).Should().Be("วิธีเปรียบเทียบราคาตลาด, วิธีต้นทุน");
        ExternalBookBuilder.SelectedApproachLine([]).Should().BeNull();
    }
}
