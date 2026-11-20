using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Section 7: the value box is a ruled 3-column table (label : amount); the certification paragraph and the signers are in neither book.</summary>
public class BookConclusionValueTableTests
{
    private const string Internal =
        "{{ include 'partials/book-funcs' }}{{ concl_no = '7.'; concl_id = 's7'; concl_machine = false; concl_construction = false; concl_block = false; include 'partials/book-conclusion' }}";
    private const string Machine =
        "{{ include 'partials/book-funcs' }}{{ concl_no = '4.'; concl_id = 's4'; concl_machine = true; concl_construction = false; concl_block = false; include 'partials/book-conclusion' }}";

    private static async Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static AppraisalSummaryModel Model(decimal? rate = 70m, bool external = false, decimal? cover = null, IReadOnlyList<AppraisalSummaryModel>? extras = null) => new()
    {
        IsExternal = external, CollateralValue = 10_000_000m, TotalAppraisalValue = 10_000_000m, ForcedSaleValue = 7_000_000m, ForceSaleRate = rate,
        BuildingCoverageAmount = cover, CompanyName = "บริษัทตัวอย่าง จำกัด", ExtraSummaryForms = extras ?? []
    };

    [Fact]
    public async Task The_value_box_is_a_table_with_a_market_and_a_forced_sale_row_each_with_figures_and_baht_text()
    {
        var html = await RenderAsync(Internal, Model());

        html.Should().Contain("<table role=\"presentation\" class=\"bk-val\">")
            .And.Contain("<div class=\"b\">มูลค่าทรัพย์สิน</div><div>(Market Value)</div>").And.Contain("<td class=\"c\">:</td>")
            .And.Contain("<td class=\"v\"><div>10,000,000.- บาท</div><div class=\"b\">(สิบล้านบาทถ้วน)</div></td>")
            .And.Contain("<div class=\"b\">มูลค่าบังคับขาย (70%)</div><div>(Forced Sale Value)</div>")
            .And.Contain("<td class=\"v\"><div>7,000,000.- บาท</div><div class=\"b\">(เจ็ดล้านบาทถ้วน)</div></td>")
            .And.NotContain("class=\"hero\"");
        html.IndexOf("(Market Value)", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("(Forced Sale Value)", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(70, "(70%)")]
    [InlineData(62.5, "(62.5%)")]
    public async Task The_force_sale_percentage_prints_without_decimals_unless_it_has_a_fraction(double rate, string expected)
    {
        var html = await RenderAsync(Internal, Model((decimal)rate));

        html.Should().Contain($"มูลค่าบังคับขาย {expected}</div>");
    }

    [Fact]
    public async Task Without_a_known_rate_the_forced_sale_label_has_no_percentage()
    {
        var html = await RenderAsync(Internal, Model(rate: null));

        html.Should().Contain("<div class=\"b\">มูลค่าบังคับขาย</div><div>(Forced Sale Value)</div>");
    }

    [Fact]
    public async Task With_extra_summary_forms_the_percentage_prints_only_when_every_form_shares_it()
    {
        var same = await RenderAsync(Internal, Model(70m, extras: [new AppraisalSummaryModel { ForceSaleRate = 70m, ForcedSaleValue = 1m }]));
        var different = await RenderAsync(Internal, Model(70m, extras: [new AppraisalSummaryModel { ForceSaleRate = 60m, ForcedSaleValue = 1m }]));

        same.Should().Contain("มูลค่าบังคับขาย (70%)</div>");
        different.Should().Contain("<div class=\"b\">มูลค่าบังคับขาย</div>").And.NotContain("(70%)").And.NotContain("(60%)");
    }

    [Fact]
    public async Task The_insurance_row_follows_show_cover_and_the_machine_book_uses_its_own_labels()
    {
        var withCover = await RenderAsync(Internal, Model(cover: 3_000_000m));
        var droppedByCostTable = await RenderAsync("{{ ins_printed = true }}" + Internal, Model(cover: 3_000_000m));
        var machine = await RenderAsync(Machine, Model(cover: 3_000_000m));

        withCover.Should().Contain("<div class=\"b\">ทุนประกันภัยสิ่งปลูกสร้าง</div><div>(Insurance Value)</div>").And.Contain("3,000,000.- บาท");
        droppedByCostTable.Should().NotContain("ทุนประกันภัยสิ่งปลูกสร้าง").And.Contain("(Forced Sale Value)");
        machine.Should().Contain("<div class=\"b\">มูลค่าตลาดเครื่องจักร</div><div>(Fair Market Value)</div>")
            .And.Contain("<div class=\"b\">ทุนประกันภัยเครื่องจักร</div><div>(Insurance Value)</div>");
    }

    [Fact]
    public async Task Null_values_print_a_dash_and_no_baht_text()
    {
        var html = await RenderAsync(Internal, new AppraisalSummaryModel { ForceSaleRate = null });

        html.Should().Contain("<td class=\"v\"><div>-.- บาท</div></td>").And.NotContain("ถ้วน");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Neither_book_prints_the_certification_paragraph_or_the_signers_so_section_7_reads_the_same(bool external)
    {
        var html = await RenderAsync(Internal, Model(external: external));

        html.Should().NotContain("ขอรับรองว่าไม่มีผลประโยชน์ใดๆ").And.NotContain("กระทำการแทนในนาม").And.NotContain("bk-sign")
            .And.NotContain("ผู้ประเมินหลัก").And.NotContain("ผู้ตรวจสอบรายงาน").And.NotContain("ผู้สำรวจและประเมินมูลค่า");
        html.Should().Contain("<table role=\"presentation\" class=\"bk-val\">");
    }
}
