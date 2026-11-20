using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// The deeds of the external book. The company letter's เอกสารสิทธิ์ที่ดิน row: numbers up to 10 deeds, a count per document type above that; the count
/// and the list agree. The สรุปผลการประเมินมูลค่าทรัพย์สิน page's ที่ดิน line reads the same deeds as running Thai text (count first, numbers grouped per type).
/// </summary>
public class BookTitleDeedSummaryTests
{
    private static List<(string?, string?)> Deeds(string type, int from, int count) => Enumerable.Range(from, count).Select(i => ((string?)type, (string?)i.ToString())).ToList();

    // ── the letter's text (SummariseTitleDeeds.Text) ────────────────────────────────────────────────────────────────

    [Fact]
    public void Ten_deeds_are_listed_with_their_numbers()
    {
        var r = ExternalBookBuilder.SummariseTitleDeeds(Deeds("DEED", 1, 10));

        r.Summarised.Should().BeFalse();
        r.Total.Should().Be(10);
        r.Text.Should().Be("โฉนดที่ดินเลขที่ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10");
    }

    [Fact]
    public void Eleven_deeds_of_two_types_become_a_count_per_type_in_first_seen_order()
    {
        var rows = Deeds("DEED", 1, 9).Concat([("NS3K", "50"), ("NS3K", "51")]).ToList();

        var r = ExternalBookBuilder.SummariseTitleDeeds(rows);

        r.Summarised.Should().BeTrue();
        r.Total.Should().Be(11);
        r.Text.Should().Be("โฉนดที่ดิน จำนวน 9 ฉบับ / น.ส.3 ก. จำนวน 2 ฉบับ");
        r.Text.Should().NotContain("เลขที่");
    }

    [Fact]
    public void Duplicates_count_once_and_a_blank_or_dash_number_is_counted_but_not_listed()
    {
        var rows = new List<(string?, string?)> { ("DEED", "1"), ("DEED", " 1 "), ("DEED", "2"), ("NS3", "1"), ("DEED", null), ("DEED", "  "), ("DEED", "-") };

        var r = ExternalBookBuilder.SummariseTitleDeeds(rows);

        r.Text.Should().Be("โฉนดที่ดินเลขที่ 1, 2 / น.ส.3 เลขที่ 1");   // the same number under another document type is another deed
        r.Total.Should().Be(6);                                          // 3 numbered + 3 without a number
        r.Unnumbered.Should().Be(3);
    }

    [Fact]
    public void Duplicates_do_not_push_a_listable_set_over_the_limit()
    {
        var rows = Deeds("DEED", 1, 10).Concat(Deeds("DEED", 1, 10)).ToList();   // 20 rows, 10 distinct deeds

        var r = ExternalBookBuilder.SummariseTitleDeeds(rows);

        r.Summarised.Should().BeFalse();
        r.Total.Should().Be(10);
    }

    [Fact]
    public void No_numbered_deed_gives_no_text_but_keeps_the_count_of_the_rows()
    {
        var r = ExternalBookBuilder.SummariseTitleDeeds([("DEED", null)]);

        r.Text.Should().BeNull();
        r.GroupedText.Should().BeNull();
        r.Total.Should().Be(1);
        ExternalBookBuilder.SummariseTitleDeeds([]).Total.Should().Be(0);
    }

    private static Task<string> LetterAsync(AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-letter-external' }}", m, CancellationToken.None);

    private static AppraisalSummaryModel Model(ExternalBookBuilder.TitleDeedSummary d, string body = "standard") =>
        new() { IsExternal = true, BodyType = body, TitleDeedNumbers = d.Text, TotalTitleDeeds = d.Total, TitleDeedsSummarised = d.Summarised, UnnumberedTitleDeeds = d.Unnumbered };

    [Fact]
    public async Task The_letter_row_lists_numbers_with_the_count_up_to_ten()
    {
        var html = await LetterAsync(Model(ExternalBookBuilder.SummariseTitleDeeds(Deeds("DEED", 1, 3))));

        html.Should().Contain("โฉนดที่ดินเลขที่ 1, 2, 3 จำนวน 3 ฉบับ").And.NotContain("รายละเอียดตามข้อ").And.NotContain("ไม่ระบุเลขที่");
    }

    [Theory]
    [InlineData("standard", "2.1")]
    [InlineData("construction", "2")]
    public async Task The_letter_row_above_ten_has_the_summary_and_the_pointer_and_no_second_count(string body, string section)
    {
        var rows = Deeds("DEED", 1, 48).Concat(Deeds("NS3K", 100, 2));
        var html = await LetterAsync(Model(ExternalBookBuilder.SummariseTitleDeeds(rows), body));

        html.Should().Contain($"โฉนดที่ดิน จำนวน 48 ฉบับ / น.ส.3 ก. จำนวน 2 ฉบับ (รายละเอียดตามข้อ {section})");
        html.Split("จำนวน 50 ฉบับ").Length.Should().Be(1);   // the template does not append a total again
    }

    [Fact]
    public async Task The_letter_row_notes_deeds_without_a_number()
    {
        var html = await LetterAsync(Model(ExternalBookBuilder.SummariseTitleDeeds([("DEED", "1"), ("DEED", null), ("DEED", "-")])));

        html.Should().Contain("โฉนดที่ดินเลขที่ 1 จำนวน 3 ฉบับ (ไม่ระบุเลขที่ 2 ฉบับ)");
    }

    [Fact]
    public async Task In_summary_mode_the_unnumbered_note_comes_before_the_pointer()
    {
        var rows = Deeds("DEED", 1, 11).Concat([("DEED", (string?)null)]);

        var html = await LetterAsync(Model(ExternalBookBuilder.SummariseTitleDeeds(rows)));

        html.Should().Contain("โฉนดที่ดิน จำนวน 11 ฉบับ (ไม่ระบุเลขที่ 1 ฉบับ) (รายละเอียดตามข้อ 2.1)");
    }

    // ── the summary page's ที่ดิน line (SummariseTitleDeeds.GroupedText + ExternalSummaryRows.DeedLine) ──────────────────

    [Theory]
    [InlineData(1, "โฉนดที่ดินเลขที่ 101")]
    [InlineData(2, "โฉนดที่ดินเลขที่ 101 และ 102")]
    [InlineData(3, "โฉนดที่ดินเลขที่ 101, 102 และ 103")]
    public void One_type_lists_its_numbers_as_A_or_A_และ_B_or_A_comma_B_และ_C(int count, string expected) =>
        ExternalBookBuilder.SummariseTitleDeeds(Deeds("DEED", 101, count)).GroupedText.Should().Be(expected);

    private static string? DeedLine(ExternalBookBuilder.TitleDeedSummary r, string body = "standard") =>
        ExternalSummaryRows.DeedLine(new AppraisalSummaryModel { BodyType = body, TitleDeedGroups = r.GroupedText, TotalTitleDeeds = r.Total, TitleDeedsSummarised = r.Summarised, UnnumberedTitleDeeds = r.Unnumbered });

    [Fact]
    public void Two_types_are_joined_with_และ_in_the_order_each_first_appeared_and_the_line_leads_with_the_count()
    {
        var rows = Deeds("DEED", 101, 3).Concat([("NS3K", "204")]).ToList();

        var r = ExternalBookBuilder.SummariseTitleDeeds(rows);

        r.GroupedText.Should().Be("โฉนดที่ดินเลขที่ 101, 102 และ 103 และ น.ส.3 ก. เลขที่ 204");
        DeedLine(r).Should().Be("ประกอบด้วยเอกสารสิทธิ์ จำนวน 4 ฉบับ ได้แก่ โฉนดที่ดินเลขที่ 101, 102 และ 103 และ น.ส.3 ก. เลขที่ 204");
        r.Text.Should().Be("โฉนดที่ดินเลขที่ 101, 102, 103 / น.ส.3 ก. เลขที่ 204");   // the letter's text is not touched by the grouping
        // a later deed of an earlier type joins that type's group, not a new one
        ExternalBookBuilder.SummariseTitleDeeds([("DEED", "1"), ("NS3K", "2"), ("DEED", "3")]).GroupedText.Should().Be("โฉนดที่ดินเลขที่ 1 และ 3 และ น.ส.3 ก. เลขที่ 2");
    }

    [Fact]
    public void One_deed_reads_the_count_of_one() =>
        DeedLine(ExternalBookBuilder.SummariseTitleDeeds(Deeds("DEED", 101, 1))).Should().Be("ประกอบด้วยเอกสารสิทธิ์ จำนวน 1 ฉบับ ได้แก่ โฉนดที่ดินเลขที่ 101");

    [Fact]
    public void Above_ten_the_line_counts_per_type_and_points_at_the_section_and_the_total_matches()
    {
        var rows = Deeds("DEED", 1, 40).Concat(Deeds("NS3K", 500, 5)).ToList();

        DeedLine(ExternalBookBuilder.SummariseTitleDeeds(rows))
            .Should().Be("ประกอบด้วยเอกสารสิทธิ์ จำนวน 45 ฉบับ ได้แก่ โฉนดที่ดิน จำนวน 40 ฉบับ และ น.ส.3 ก. จำนวน 5 ฉบับ (รายละเอียดตามข้อ 2.1)");
    }

    [Fact]
    public void The_summary_pages_unnumbered_note_comes_before_the_pointer()
    {
        var rows = Deeds("DEED", 1, 11).Concat([("DEED", (string?)null)]);

        DeedLine(ExternalBookBuilder.SummariseTitleDeeds(rows))
            .Should().Be("ประกอบด้วยเอกสารสิทธิ์ จำนวน 12 ฉบับ ได้แก่ โฉนดที่ดิน จำนวน 11 ฉบับ (ไม่ระบุเลขที่ 1 ฉบับ) (รายละเอียดตามข้อ 2.1)");
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "ก")]
    [InlineData(2, "ก และ ข")]
    [InlineData(4, "ก, ข, ค และ ง")]
    public void The_thai_list_join_puts_a_comma_between_items_and_และ_before_the_last(int count, string expected) =>
        ThaiTextList.Join(new[] { "ก", "ข", "ค", "ง" }.Take(count).ToList()).Should().Be(expected);
}
