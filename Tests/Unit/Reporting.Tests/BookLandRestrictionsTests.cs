using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Land book: 2.2 การรอนสิทธิ keeps the eviction chips and the area-deduction table; the legal rows sit in each land's 3.1 under ข้อจำกัดทางกฎหมาย.</summary>
public class BookLandRestrictionsTests
{
    private static Task<string> RenderAsync(AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync("{{ include 'partials/book-flow-standard' }}", model, CancellationToken.None);

    private static AppraisalSummaryModel Model(IReadOnlyList<LandSection> lands, IReadOnlyList<LawRecord>? laws = null) => new()
    {
        PropertyGroups = [new PropertyGroupDetail { Lands = lands }],
        LawRecords = laws ?? []
    };

    private static string Between(string html, string from, string to)
    {
        var start = html.IndexOf(from, StringComparison.Ordinal);
        return html[start..html.IndexOf(to, start, StringComparison.Ordinal)];
    }

    private static string Section22(string html) => Between(html, "<span>2.2</span>", "<span>2.3</span>");

    private static string Section4(string html) => html[html.IndexOf("<span>4.</span>", StringComparison.Ordinal)..];

    private static string Section31(string html) => Between(html, "<span>3.1</span>", "<span>4.</span>");

    private static string LegalBlock(string html)
    {
        var s31 = Section31(html);
        return s31[s31.IndexOf("ข้อจำกัดของหลักประกัน", StringComparison.Ordinal)..];
    }

    private static readonly string[] LegalLabels = ["แนวเวนคืน", "แนวเขตป่าไม้"];

    // Their detail lives in the Law & Regulation records (section 4); landlocked is การเข้าถึงที่ดิน in 1.3.
    private static readonly string[] DroppedLabels = ["พระราชกฤษฎีกาเวนคืน", "ที่ดินตาบอด", "การบุกรุก"]; // การบุกรุก: IsEncroached is the deduction toggle, 2.1 shows the table

    private static LandSection LandWithEverything() => new()
    {
        EvictionType = "อยู่ในแนวสายไฟฟ้าแรงสูง, ริมแม่น้ำเจ้าพระยา",
        IsInExpropriationLine = false, RoyalDecree = "พ.ร.ฎ. ตัวอย่าง", IsEncroached = true, EncroachmentRemark = "รั้วล้ำ",
        IsLandlocked = false, IsForestBoundary = false, OtherLegalLimitations = "ข้อจำกัดอื่น",
        DeductedAreaInWa = 66.5m,
        Deductions =
        [
            new LandDeductionRow { Sequence = 1, Reason = "ถูกรุกล้ำ – รั้ว/กำแพง", AreaInSqWa = 20m, Remark = "ล้ำเข้ามา 2 เมตร" },
            new LandDeductionRow { Sequence = 2, Reason = "ริมแม่น้ำเจ้าพระยา (ที่กำหนดเอง)", AreaInSqWa = 46.5m }
        ],
        Titles = [new LandTitleRow { TitleNumber = "12345", AreaRai = 1, AreaNgan = 0, AreaSquareWa = 0 }]
    };

    [Fact]
    public async Task Section_2_2_prints_the_eviction_chips_including_the_other_text()
    {
        var html = Section22(await RenderAsync(Model([LandWithEverything()])));

        html.Should().Contain("<span class=\"l\">การรอนสิทธิ์</span><span class=\"c\">:</span><span class=\"v\">อยู่ในแนวสายไฟฟ้าแรงสูง, ริมแม่น้ำเจ้าพระยา</span>");
    }

    private static string Section21(string html) => Between(html, "<span>2.1</span>", "<span>2.2</span>");

    [Fact]
    public async Task The_deduction_table_sits_under_the_2_1_title_table_with_its_total_equal_to_the_deducted_row()
    {
        var html = await RenderAsync(Model([LandWithEverything()]));
        var s21 = Section21(html);

        s21.Should().Contain("รายละเอียดเนื้อที่ที่ตัดออกจากการประเมิน")
            .And.Contain("<th>เหตุผล</th>").And.Contain("<th>เนื้อที่ (ตร.ว.)</th>").And.Contain("<th>หมายเหตุ</th>")
            .And.Contain("<tr><td class=\"c\">1</td><td class=\"c\">ถูกรุกล้ำ – รั้ว/กำแพง</td><td class=\"c\">20</td><td class=\"c\">ล้ำเข้ามา 2 เมตร</td></tr>")
            .And.Contain("<tr><td class=\"c\">2</td><td class=\"c\">ริมแม่น้ำเจ้าพระยา (ที่กำหนดเอง)</td><td class=\"c\">46.5</td><td class=\"c\"></td></tr>")
            .And.Contain("<td colspan=\"2\" class=\"r\">รวม</td><td class=\"c\">66.5</td>")
            .And.NotContain("<th>ที่ดิน</th>");                      // one land: no land column
        s21.Should().Contain("<td class=\"c\">66.5 ตารางวา</td>").And.NotContain("<td colspan=\"3\" class=\"c\">66.5</td>");   // the หัก row of the title table above shows the same figure
        s21.IndexOf("รายละเอียดเนื้อที่ที่ตัดออกจากการประเมิน", StringComparison.Ordinal)
            .Should().BeGreaterThan(s21.IndexOf("คงเหลือ เนื้อที่ที่ใช้ประเมิน", StringComparison.Ordinal))
            .And.BeLessThan(s21.IndexOf("ระวาง", s21.IndexOf("</table>", StringComparison.Ordinal), StringComparison.Ordinal) + 100000);
        Section22(html).Should().NotContain("<table").And.NotContain("เหตุผล"); // and no longer in 2.2
    }

    [Fact]
    public async Task With_several_lands_there_is_one_combined_table_with_a_land_column_and_continuing_numbers()
    {
        var html = await RenderAsync(Model([
            new LandSection { DeductedAreaInWa = 10m, Deductions = [new LandDeductionRow { Sequence = 1, Reason = "ก", AreaInSqWa = 10m }], Titles = [new LandTitleRow { TitleNumber = "1" }] },
            new LandSection { DeductedAreaInWa = 25m, Deductions = [new LandDeductionRow { Sequence = 1, Reason = "ข", AreaInSqWa = 15m }, new LandDeductionRow { Sequence = 2, Reason = "ค", AreaInSqWa = 10m }], Titles = [new LandTitleRow { TitleNumber = "2" }] }]));
        var s21 = Section21(html);

        s21.Should().Contain("<th>ที่ดิน</th>");
        s21.Split("รายละเอียดเนื้อที่ที่ตัดออกจากการประเมิน").Length.Should().Be(2); // one table
        System.Text.RegularExpressions.Regex.Matches(s21, @"<td class=""c"">([123])</td><td class=""c"">[กขค]</td>").Select(m => m.Groups[1].Value)
            .Should().Equal("1", "2", "3"); // ลำดับ continues across lands
        s21.Should().Contain("<td colspan=\"3\" class=\"r\">รวม</td><td class=\"c\">35</td>"); // 10 + 25 = the หัก row
    }

    [Fact]
    public async Task Section_2_1_has_no_deduction_table_when_no_land_has_deductions()
    {
        var html = await RenderAsync(Model([new LandSection { EvictionType = "แนวรถไฟฟ้าใต้ดิน", Titles = [new LandTitleRow { TitleNumber = "1" }] }]));

        Section21(html).Should().NotContain("รายละเอียดเนื้อที่ที่ตัดออกจากการประเมิน").And.NotContain("เหตุผล");
        Section22(html).Should().Contain("แนวรถไฟฟ้าใต้ดิน").And.NotContain("<table");
    }

    [Theory]
    [InlineData("เส้นทางผ่านกลางแปลง", true)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public async Task The_eviction_remark_prints_under_the_chips_only_when_it_has_text(string? remark, bool shown)
    {
        var html = Section22(await RenderAsync(Model([new LandSection { EvictionType = "แนวท่อส่งก๊าซธรรมชาติ", EvictionRemark = remark, Titles = [new LandTitleRow { TitleNumber = "1" }] }])));

        html.Should().Contain("<span class=\"v\">แนวท่อส่งก๊าซธรรมชาติ" + (shown ? $"<span class=\"rm\">หมายเหตุ : {remark}</span>" : "</span>"));
    }

    [Fact]
    public async Task A_remark_with_no_chips_still_prints_as_the_value()
    {
        var html = Section22(await RenderAsync(Model([new LandSection { EvictionRemark = "รอตรวจสอบแนวเขต", Titles = [new LandTitleRow { TitleNumber = "1" }] }])));

        html.Should().Contain("<span class=\"rm\">หมายเหตุ : รอตรวจสอบแนวเขต</span>");
    }

    [Fact]
    public async Task The_legal_rows_are_gone_from_2_2_and_section_4_and_sit_in_3_1_in_order()
    {
        var land = new LandSection
        {
            EvictionType = "อยู่ในแนวสายไฟฟ้าแรงสูง", IsInExpropriationLine = false, RoyalDecree = "พ.ร.ฎ. ตัวอย่าง", IsEncroached = true, EncroachmentRemark = "รั้วล้ำ",
            IsLandlocked = false, IsForestBoundary = false, LandDescription = "สี่เหลี่ยมผืนผ้า", LandDescriptionText = "ติดถนน", OtherLegalLimitations = "ข้อจำกัดอื่น",
            Boundaries = [new LandBoundaryRow { Direction = "ทิศเหนือ", Adjacent = "ถนน" }],
            Titles = [new LandTitleRow { TitleNumber = "12345", AreaRai = 1 }]
        };

        var html = await RenderAsync(Model([land]));

        foreach (var label in LegalLabels)
        {
            Section22(html).Should().NotContain($"<span class=\"l\">{label}");
            Section4(html).Should().NotContain($"<span class=\"l\">{label}");
            Section31(html).Should().Contain($"<span class=\"l\">{label}");
        }
        Section4(html).Should().NotContain("ข้อจำกัดของหลักประกัน").And.NotContain("ข้อจำกัดทางกฎหมาย");
        foreach (var label in DroppedLabels)
            html.Should().NotContain($"<span class=\"l\">{label}");
        var s31 = Section31(html);
        new[] { "รูปแปลงที่ดิน", ">ลักษณะทางกายภาพ<", ">อาณาเขต<", ">ข้อจำกัดของหลักประกัน<", ">ข้อจำกัดอื่นๆ ที่พบ<" }
            .Select(m => s31.IndexOf(m, StringComparison.Ordinal)).Should().BeInAscendingOrder().And.NotContain(-1);
        s31.Should().NotContain("การใช้ประโยชน์ปัจจุบัน"); // 1.2 already prints the land use
        html.Should().NotContain("รั้วล้ำ"); // the encroachment remark is not printed any more
    }

    [Fact]
    public async Task The_fallback_section_4_is_4_1_and_4_2_only()
    {
        var s4 = Section4(await RenderAsync(Model([LandWithEverything()])));

        s4.Should().Contain("<span>4.1</span>").And.Contain("<span>4.2</span>").And.NotContain("<span>4.3</span>").And.NotContain("ข้อจำกัดทางกฎหมาย");
    }

    [Fact]
    public async Task With_law_records_section_4_has_no_extra_legal_heading()
    {
        var laws = new[] { new LawRecord { Topic = "ผังเมือง" }, new LawRecord { Topic = "เขตอนุรักษ์" } };

        var s4 = Section4(await RenderAsync(Model([LandWithEverything()], laws)));

        s4.Should().NotContain("ข้อจำกัดของหลักประกัน").And.NotContain("ข้อจำกัดทางกฎหมาย").And.NotContain("<span>4.3</span>");
    }

    [Fact]
    public async Task Expropriation_and_forest_boundary_are_yes_no_only_in_both_branches_their_detail_is_not_printed_here()
    {
        var land = new LandSection
        {
            IsInExpropriationLine = true, ExpropriationRemark = "ถูกเวนคืนบางส่วนตามแผนถนนสายใหม่", RoyalDecree = "พ.ร.ฎ. ตัวอย่าง",
            IsForestBoundary = true, ForestBoundaryRemark = "ติดเขตป่าสงวน", Titles = [new LandTitleRow { TitleNumber = "1" }]
        };

        var fallback = await RenderAsync(Model([land]));
        var withLaws = await RenderAsync(Model([land], [new LawRecord { Topic = "ผังเมือง" }]));

        foreach (var html in new[] { fallback, withLaws })
        {
            var legal = LegalBlock(html);
            legal.Should().Contain("<span class=\"v\">อยู่ในแนวเวนคืน</span>").And.Contain("<span class=\"v\">อยู่</span>")
                .And.NotContain("ถนนสายใหม่").And.NotContain("พ.ร.ฎ.").And.NotContain("ติดเขตป่าสงวน");
        }
    }

    [Fact]
    public async Task A_remark_with_no_expropriation_answer_prints_nothing_extra()
    {
        var html = await RenderAsync(Model([new LandSection { ExpropriationRemark = "รอผลสำรวจ", Titles = [new LandTitleRow { TitleNumber = "1" }] }]));

        LegalBlock(html).Should().NotContain("รอผลสำรวจ").And.NotContain("class=\"rm\"");
    }

    [Fact]
    public async Task Each_land_prints_its_own_rows_in_2_2_and_its_own_legal_block_in_3_1()
    {
        var html = await RenderAsync(Model([
            new LandSection { EvictionType = "แปลงที่หนึ่ง", IsEncroached = true, Titles = [new LandTitleRow { TitleNumber = "1" }] },
            new LandSection { EvictionType = "แปลงที่สอง", IsEncroached = false, Titles = [new LandTitleRow { TitleNumber = "2" }] }]));

        Section22(html).Should().Contain("แปลงที่หนึ่ง").And.Contain("แปลงที่สอง");
        var s31 = Section31(html);
        s31.Split(">ข้อจำกัดของหลักประกัน<").Length.Should().Be(3);                  // one block per land
        s31.Should().NotContain("กฎหมาย");                                             // the heading no longer says กฎหมาย
        s31.Should().NotContain("การบุกรุก");
    }
}
