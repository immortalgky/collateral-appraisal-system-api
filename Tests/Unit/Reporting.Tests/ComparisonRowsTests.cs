using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Infrastructure.Rendering;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;
using FactorRef = Reporting.Application.Providers.Sections.ComparisonSectionLoader.FactorRef;
using ComparableRow = Reporting.Application.Providers.Sections.ComparisonSectionLoader.ComparableRow;

namespace Reporting.Tests;

/// <summary>Book §8: the factor rows follow the factors the group's pricing methods use; static rows print only when filled.</summary>
public class ComparisonRowsTests
{
    private static ComparableRow C(decimal? offer = null, decimal? sale = null, DateTime? saleDate = null, string? source = null, string? survey = null, string? notes = null) =>
        new() { Id = Guid.NewGuid(), OfferPrice = offer, SalePrice = sale, SaleDate = saleDate, SourceInfo = source, SurveyName = survey, Notes = notes, InfoDateTime = new DateTime(2026, 1, 5) };

    private static ComparisonFactorRow F(string code, params string?[] values) => new() { FactorCode = code, FactorName = "x", Values = values };

    private static string[] Labels(IReadOnlyList<ComparisonFactorRow> rows) => rows.Select(r => r.FactorName!).ToArray();

    [Fact]
    public void Union_is_the_first_methods_order_then_extras_without_duplicates()
    {
        var wqs = new FactorRef[] { new("03", "สภาพที่ดิน"), new("05", "หน้ากว้าง"), new("14", "พื้นที่ใช้สอย") };
        var grid = new FactorRef[] { new("05", "หน้ากว้าง"), new("26", "ผิวจราจร"), new("03", "สภาพที่ดิน"), new("32", "สิ่งอำนวยฯ") };

        var union = ComparisonSectionLoader.UnionFactors([wqs, grid]);

        union.Select(f => f.Code).Should().Equal("03", "05", "14", "26", "32");
    }

    [Fact]
    public void Rows_follow_the_method_factors_in_order_with_their_display_names_and_the_subject_value()
    {
        var comparables = new[] { C(), C() };
        var rows = ComparisonSectionLoader.BuildFsdRows(comparables,
            [F("05", "20", "25"), F("03", "ถมแล้ว", null)],
            new Dictionary<string, string?> { ["05"] = "30" },
            [new("05", "หน้ากว้างที่ดินติดถนน"), new("03", "สภาพที่ดิน")]);

        Labels(rows).Skip(1).Take(2).Should().Equal("หน้ากว้างที่ดินติดถนน", "สภาพที่ดิน"); // after the property-type row
        rows[1].Values.Should().Equal("20", "25");
        rows[1].SubjectValues.Should().Equal("30");
        rows[2].SubjectValues.Should().Equal((string?)null);
        Labels(rows).Should().NotContain("ผิวจราจร"); // a factor no method uses is not printed
    }

    [Fact]
    public void Static_rows_follow_the_factors_and_print_only_when_some_comparable_has_a_value()
    {
        var factors = new FactorRef[] { new("03", "สภาพที่ดิน") };
        var empty = ComparisonSectionLoader.BuildFsdRows([C(), C()], [F("03", "a", "b")], null, factors);
        var filled = ComparisonSectionLoader.BuildFsdRows(
            [C(offer: 1_000_000m, sale: 900_000m, saleDate: new DateTime(2026, 2, 3), source: "นายก", notes: "ติดถนน"), C()],
            [F("03", "a", "b")], null, factors);

        // nothing recorded but the info date (always present on a comparable): the only static row
        Labels(empty).Should().Equal("ประเภททรัพย์สิน", "สภาพที่ดิน", "วันที่ได้รับข้อมูล");
        Labels(filled).Should().Equal("ประเภททรัพย์สิน", "สภาพที่ดิน", "ราคาเสนอขาย", "ราคาซื้อขาย", "วันที่ซื้อขาย", "วันที่ได้รับข้อมูล", "แหล่งที่มาข้อมูล", "หมายเหตุ");
        filled.Single(r => r.FactorName == "ราคาเสนอขาย").Values.Should().Equal("1,000,000", null);
    }

    [Fact]
    public void Source_is_SourceInfo_only_when_filled_and_the_survey_name_is_never_printed()
    {
        var factors = new FactorRef[] { new("03", "สภาพที่ดิน") };
        var filled = ComparisonSectionLoader.BuildFsdRows([C(source: "ป้ายประกาศ", survey: "ตลาด 1"), C(survey: "ตลาด 2")], [], null, factors);
        var surveyOnly = ComparisonSectionLoader.BuildFsdRows([C(survey: "ตลาด 1")], [], null, factors);

        filled.Single(r => r.FactorName == "แหล่งที่มาข้อมูล").Values.Should().Equal("ป้ายประกาศ", null);
        Labels(filled).Should().NotContain("ผู้ให้ข้อมูล / เบอร์ติดต่อ");
        filled.SelectMany(r => r.Values).Should().NotContain("ตลาด 1").And.NotContain("ตลาด 2");
        Labels(surveyOnly).Should().NotContain("แหล่งที่มาข้อมูล"); // a survey name alone is not a source
    }

    [Fact]
    public void Rai_ngan_wa_factors_merge_into_one_land_area_row_at_the_first_of_them()
    {
        var rows = ComparisonSectionLoader.BuildFsdRows([C()],
            [F("43", "1"), F("44", "2"), F("45", "50")], null,
            [new("03", "สภาพที่ดิน"), new("43", "ไร่"), new("44", "งาน"), new("45", "ตร.วา"), new("05", "หน้ากว้าง")]);

        Labels(rows).Skip(1).Take(3).Should().Equal("สภาพที่ดิน", "เนื้อที่ดิน", "หน้ากว้าง");
        rows[2].Values.Should().Equal("1-2-50");
        Labels(rows).Should().NotContain("ไร่").And.NotContain("งาน");
    }

    [Fact]
    public void Property_type_is_always_the_first_bold_row_from_the_comparables_own_type_and_the_subjects_text()
    {
        // no method uses factor 01, yet the row is there; the comparable's own (decoded) type wins over any factor-01 value
        var rows = ComparisonSectionLoader.BuildFsdRows([C(), C()], [F("01", "ค่าจากปัจจัย", "ค่าจากปัจจัย")],
            new Dictionary<string, string?> { ["01"] = "ที่ดินพร้อมสิ่งปลูกสร้าง" }, [new("03", "สภาพที่ดิน")],
            ["ที่ดินเปล่า", null]);

        rows[0].FactorName.Should().Be("ประเภททรัพย์สิน");
        rows[0].Bold.Should().BeTrue();
        rows[0].Values.Should().Equal("ที่ดินเปล่า", "ค่าจากปัจจัย"); // the second comparable has no own type: its factor-01 value
        rows[0].SubjectValues.Should().Equal("ที่ดินพร้อมสิ่งปลูกสร้าง");
    }

    [Fact]
    public void Property_type_is_not_printed_twice_when_factor_01_is_among_the_method_factors()
    {
        var rows = ComparisonSectionLoader.BuildFsdRows([C()], [F("01", "x")], null,
            [new("01", "ประเภททรัพย์สิน"), new("03", "สภาพที่ดิน")], ["ที่ดินเปล่า"]);

        Labels(rows).Count(l => l == "ประเภททรัพย์สิน").Should().Be(1);
        Labels(rows).Should().StartWith("ประเภททรัพย์สิน", "สภาพที่ดิน");
    }

    [Fact]
    public async Task A_bold_row_is_bold_in_every_cell_label_subject_and_comparables()
    {
        var table = new ComparisonTable
        {
            ComparableHeaders = ["ข้อมูล 1"], SubjectHeaders = ["ทรัพย์สินที่ประเมิน"],
            FsdRows =
            [
                new ComparisonFactorRow { FactorName = "ประเภททรัพย์สิน", Bold = true, SubjectValues = ["ที่ดิน"], Values = ["ที่ดินเปล่า"] },
                new ComparisonFactorRow { FactorName = "สภาพที่ดิน", SubjectValues = ["ถมแล้ว"], Values = ["ถมแล้ว"] }
            ]
        };

        var html = await RenderAsync("{{ cs = model.comparison_sections[0] }}{{ include 'partials/section-comparison' }}",
            new AppraisalSummaryModel { ComparisonSections = [new ComparisonSection { Tables = [table] }] });

        System.Text.RegularExpressions.Regex.IsMatch(html, @"<tr style=""font-weight:700;"">\s*<td>ประเภททรัพย์สิน</td>").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(html, @"<tr>\s*<td>สภาพที่ดิน</td>").Should().BeTrue();
        html.Should().NotContain("<td style=\"font-weight:700;\">"); // one row-level style, not per cell
    }

    // ── Section 8 is dropped with its contents entry when no group has comparative factors ──

    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static AppraisalSummaryModel WithComparison(string bodyType, bool has) => new()
    {
        BodyType = bodyType,
        BlockSection = bodyType == "block" ? new BlockBookSection() : null,
        ComparisonSections = has ? [new ComparisonSection { Tables = [] }] : []
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_standard_book_has_section_8_and_its_contents_entry_only_with_comparison_data(bool has)
    {
        var body = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-tail' }}", WithComparison("standard", has));
        var toc = await RenderAsync("{{ include 'partials/book-front-matter' }}", WithComparison("standard", has));

        body.Contains("<span>8.</span><span>ข้อมูลตลาดเปรียบเทียบ</span>").Should().Be(has);
        toc.Contains("8. ข้อมูลตลาดเปรียบเทียบ").Should().Be(has);
        toc.Should().Contain("7. สรุปการประเมินมูลค่าทรัพย์สิน"); // section 7 is last when 8 is gone
    }

    [Theory]
    [InlineData(false, "6. สรุปการประเมินมูลค่าทรัพย์สิน")]
    [InlineData(true, "7. สรุปการประเมินมูลค่าทรัพย์สิน")]
    public async Task The_block_book_closes_on_the_next_number_when_its_comparison_section_is_dropped(bool has, string expected)
    {
        var toc = await RenderAsync("{{ include 'partials/book-front-matter' }}", WithComparison("block", has));

        toc.Should().Contain(expected);
        toc.Contains("6. ข้อมูลตลาดเปรียบเทียบ").Should().Be(has);
    }
}
