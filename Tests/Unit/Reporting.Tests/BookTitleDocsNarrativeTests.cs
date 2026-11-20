using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book 2.1: the sentence built from the document check result and the land office, and the centred title table with its ตารางวา totals.</summary>
public class BookTitleDocsNarrativeTests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static LandTitleRow T(string number, string? result, decimal rai = 0, decimal ngan = 0, decimal wa = 0) =>
        new() { TitleNumber = number, DocumentValidationResult = result, AreaRai = rai, AreaNgan = ngan, AreaSquareWa = wa };

    private static Task<string> RenderLandsAsync(params LandSection[] lands) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-title-docs' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = lands }] });

    private static string Sentence(string html)
    {
        var start = html.IndexOf("<p class=\"bk-p\">", StringComparison.Ordinal) + "<p class=\"bk-p\">".Length;
        return html[start..html.IndexOf("</p>", start, StringComparison.Ordinal)];
    }

    private const string Intro = "เอกสารสิทธิ์ในการประเมินนี้ได้รับสำเนาจากผู้ว่าจ้าง และได้ตรวจสอบเทียบกับเอกสารสิทธิ์ฉบับสำนักงานที่ดิน";

    [Fact]
    public async Task All_titles_matching_say_so()
    {
        var html = await RenderLandsAsync(new LandSection { LandOffice = "กรุงเทพมหานคร สาขาบางเขน", Titles = [T("12345", "01"), T("12346", "01")] });

        Sentence(html).Should().Be(Intro + " โดยตรวจสอบ ณ <b>สำนักงานที่ดินกรุงเทพมหานคร สาขาบางเขน</b> ปรากฏว่าถูกต้องตรงกัน รายละเอียดดังต่อไปนี้");
    }

    [Fact]
    public async Task All_titles_mismatching_say_so()
    {
        var html = await RenderLandsAsync(new LandSection { LandOffice = "สำนักงานที่ดินจังหวัดตัวอย่าง", Titles = [T("12345", "02")] });

        Sentence(html).Should().Contain("ปรากฏว่าไม่สอดคล้องกัน รายละเอียดดังต่อไปนี้").And.NotContain("ถูกต้องตรงกัน");
    }

    [Fact]
    public async Task Mixed_results_list_the_titles_of_each_across_all_lands()
    {
        var html = await RenderLandsAsync(
            new LandSection { LandOffice = "จังหวัดน่าน", Titles = [T("12345", "01"), T("12346", "01")] },
            new LandSection { LandOffice = "จังหวัดสงขลา", Titles = [T("12347", "02")] });

        Sentence(html).Should().Contain("โดยตรวจสอบ ณ <b>สำนักงานที่ดินจังหวัดน่าน และ สำนักงานที่ดินจังหวัดสงขลา</b>")
            .And.Contain("ปรากฏว่าเอกสารสิทธิ์เลขที่ 12345, 12346 ถูกต้องตรงกัน และเลขที่ 12347 ไม่สอดคล้องกัน รายละเอียดดังต่อไปนี้");
    }

    [Fact]
    public async Task A_bare_office_name_gets_the_prefix_and_a_prefixed_one_is_not_doubled()
    {
        var bare = await RenderLandsAsync(new LandSection { LandOffice = "กรุงเทพมหานคร สาขาบางเขน", Titles = [T("12345", "01")] });
        var prefixed = await RenderLandsAsync(new LandSection { LandOffice = "สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา", Titles = [T("12345", "01")] });

        Sentence(bare).Should().Contain("ณ <b>สำนักงานที่ดินกรุงเทพมหานคร สาขาบางเขน</b>");
        Sentence(prefixed).Should().Contain("ณ <b>สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา</b>").And.NotContain("สำนักงานที่ดินสำนักงานที่ดิน");
    }

    [Fact]
    public async Task Office_name_and_title_numbers_are_escaped_exactly_once_and_the_bold_tag_stays_raw()
    {
        var html = await RenderLandsAsync(new LandSection { LandOffice = "A&B <x>", Titles = [T("1<2", "01"), T("3&4", "02")] });

        Sentence(html).Should().Contain("ณ <b>สำนักงานที่ดินA&amp;B &lt;x&gt;</b>")
            .And.Contain("เอกสารสิทธิ์เลขที่ 1&lt;2 ถูกต้องตรงกัน และเลขที่ 3&amp;4 ไม่สอดคล้องกัน")
            .And.NotContain("&amp;amp;").And.NotContain("&amp;lt;");
    }

    [Fact]
    public async Task Two_offices_each_get_the_prefix_and_are_joined_with_and()
    {
        var html = await RenderLandsAsync(
            new LandSection { LandOffice = "กรุงเทพมหานคร สาขาบางเขน", Titles = [T("12345", "01")] },
            new LandSection { LandOffice = "สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา", Titles = [T("12346", "01")] });

        Sentence(html).Should().Contain("ณ <b>สำนักงานที่ดินกรุงเทพมหานคร สาขาบางเขน และ สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา</b>");
    }

    [Fact]
    public async Task No_result_drops_the_appears_part_and_no_office_drops_the_checked_at_part()
    {
        var noResult = await RenderLandsAsync(new LandSection { LandOffice = "จังหวัดน่าน", Titles = [T("12345", null)] });
        var noOffice = await RenderLandsAsync(new LandSection { Titles = [T("12345", "01")] });

        Sentence(noResult).Should().Be(Intro + " โดยตรวจสอบ ณ <b>สำนักงานที่ดินจังหวัดน่าน</b> รายละเอียดดังต่อไปนี้");
        Sentence(noOffice).Should().Be(Intro + " ปรากฏว่าถูกต้องตรงกัน รายละเอียดดังต่อไปนี้");
    }

    [Theory]
    [InlineData("01", "ถูกต้องตรงกัน")]
    [InlineData("02", "ไม่สอดคล้องกัน")]
    public async Task Condo_uses_the_condo_ownership_document_name(string result, string text)
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-condo' }}",
            new AppraisalSummaryModel { CondoSection = new CondoSection { LandOffice = "กรุงเทพมหานคร สาขาบางเขน", DocumentValidationResult = result } });

        var s = Sentence(html.Substring(html.IndexOf("รายละเอียดหนังสือกรรมสิทธิ์ห้องชุด", StringComparison.Ordinal)));
        s.Should().Be("หนังสือกรรมสิทธิ์ห้องชุดในการประเมินนี้ได้รับสำเนาจากผู้ว่าจ้าง และได้ตรวจสอบเทียบกับหนังสือกรรมสิทธิ์ห้องชุดฉบับสำนักงานที่ดิน"
            + $" โดยตรวจสอบ ณ <b>สำนักงานที่ดินกรุงเทพมหานคร สาขาบางเขน</b> ปรากฏว่า{text} รายละเอียดดังต่อไปนี้");
    }

    [Theory]
    [InlineData("01", "01")]
    [InlineData(" 02 ", "02")]
    [InlineData("1", null)]   // older rows hold '1' or blank: no result
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Only_the_two_defined_codes_count_as_a_result(string? code, string? expected)
        => DocumentCheck.Result(code).Should().Be(expected);

    [Fact]
    public async Task Title_table_cells_are_centred_and_the_totals_also_show_square_wa()
    {
        // 2 rai 2 ngan 66 wa = 1066 wa; 66 wa deducted leaves 1000 wa = 2-2-0
        var html = await RenderLandsAsync(new LandSection { DeductedAreaInWa = 66m, Titles = [T("12345", null, 2, 2, 66)] });

        html.Should().Contain("<td class=\"c\">12345</td>").And.Contain("<td class=\"c\">2</td>").And.Contain("<td class=\"c\">66</td>");
        // the totals' numbers are centred like the column above them; only the row labels stay right-aligned
        html.Should().Contain("<td class=\"c\">2</td><td class=\"c\">2</td><td class=\"c\">66</td><td class=\"c\">หรือ 1,066 ตารางวา</td>");
        // the หัก row: the three ไร่ / งาน / วา cells stay empty, its value sits in the last column, in ตารางวา like the totals around it
        html.Should().Contain("<tr class=\"ded\"><td colspan=\"4\" class=\"r\">หัก เนื้อที่ที่ตัดออกจากการประเมิน</td><td class=\"c\"></td><td class=\"c\"></td><td class=\"c\"></td><td class=\"c\">66 ตารางวา</td></tr>")
            .And.NotContain("ที่ตัดออกจากการประเมิน (ตารางวา)");
        html.Should().Contain("คงเหลือ เนื้อที่ที่ใช้ประเมิน</td><td class=\"c\">2</td><td class=\"c\">2</td><td class=\"c\">0</td><td class=\"c\">หรือ 1,000 ตารางวา</td>");
        html.Should().Contain("<td colspan=\"4\" class=\"r\">เนื้อที่รวมตามเอกสารสิทธิ์</td>");
    }

    [Fact]
    public async Task Without_a_deduction_there_is_no_remaining_row()
    {
        var html = await RenderLandsAsync(new LandSection { Titles = [T("12345", null, 1, 0, 0)] });

        html.Should().Contain("หรือ 400 ตารางวา").And.NotContain("คงเหลือ");
    }
}
