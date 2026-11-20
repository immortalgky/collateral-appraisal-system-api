using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Section 5.1: the method narrative (one builder, one phrase table) and the 5.2 assumptions paragraph.</summary>
public class PricingNarrativeTests
{
    private static ApproachValueRow Row(string type, bool reference = false, int group = 1, params (string Method, string? Role)[] methods) => new()
    {
        GroupNumber = group, ApproachType = type, IsReference = reference, Methods = methods.Select(m => new ApproachMethodRef(m.Method, m.Role)).ToList()
    };

    private const string CostLandWqsBuilding =
        "ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้วิธีต้นทุน (Cost Approach) ในการสรุปมูลค่า โดยประเมินมูลค่าที่ดินด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS) " +
        "และประเมินมูลค่าสิ่งปลูกสร้างด้วยวิธีต้นทุนทดแทนหักค่าเสื่อมราคา";

    [Fact]
    public void A_single_market_approach_names_the_method_on_the_whole_property_and_has_no_reference_paragraph()
    {
        var p = PricingNarrative.Build([Row("Market", methods: ("WQS", null))], multiGroup: false);

        p.Should().Equal("ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้วิธีเปรียบเทียบตลาด (Market Approach) ในการสรุปมูลค่า โดยประเมินมูลค่าทรัพย์สินด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS)");
    }

    [Fact]
    public void A_cost_approach_with_land_and_building_lists_the_two_parts_land_first_and_ends_right_after_the_building_part()
    {
        // the rows arrive building first: the narrative still lists the land first
        var p = PricingNarrative.Build([Row("Cost", methods: [("BuildingCost", "Building"), ("WQS", "Land")])], false);

        p.Should().Equal(CostLandWqsBuilding);
        p[0].Should().NotContain("มารวมกัน").And.NotEndWith(" ");   // no sum clause, and the sentence ends cleanly
    }

    [Fact]
    public void A_cost_approach_with_the_building_only_ends_after_the_building_part()
    {
        var p = PricingNarrative.Build([Row("Cost", methods: ("BuildingCost", "Building"))], false);

        p.Should().Equal("ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้วิธีต้นทุน (Cost Approach) ในการสรุปมูลค่า โดยประเมินมูลค่าสิ่งปลูกสร้างด้วยวิธีต้นทุนทดแทนหักค่าเสื่อมราคา");
    }

    [Fact]
    public void A_reference_approach_adds_the_second_paragraph_and_without_one_there_is_none()
    {
        var withReference = PricingNarrative.Build(
            [Row("Cost", methods: [("WQS", "Land"), ("BuildingCost", "Building")]), Row("Market", reference: true, methods: ("WQS", null))], false);
        var without = PricingNarrative.Build([Row("Cost", methods: [("WQS", "Land"), ("BuildingCost", "Building")])], false);

        withReference.Should().Equal(CostLandWqsBuilding, "ทั้งนี้ ได้ใช้วิธีเปรียบเทียบตลาด (Market Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา");
        without.Should().Equal(CostLandWqsBuilding);
    }

    [Fact]
    public void Several_reference_approaches_are_listed_and_only_reference_approaches_drop_the_thathi()
    {
        var several = PricingNarrative.Build(
            [Row("Market", methods: ("WQS", null)), Row("Cost", reference: true), Row("Income", reference: true)], false);
        var onlyReference = PricingNarrative.Build([Row("Income", reference: true)], false);

        several[1].Should().Be("ทั้งนี้ ได้ใช้วิธีต้นทุน (Cost Approach) และวิธีรายได้ (Income Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา");
        onlyReference.Should().Equal("ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้วิธีรายได้ (Income Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา");
    }

    [Fact]
    public void Each_group_of_a_multi_group_appraisal_starts_with_its_number_and_its_reference_sentence_follows_it_unprefixed()
    {
        var p = PricingNarrative.Build(
        [
            Row("Cost", group: 2, methods: [("SaleGrid", "Land"), ("BuildingCost", "Building")]),
            Row("Market", reference: true, group: 2, methods: ("WQS", null)),
            Row("Market", group: 1, methods: ("DirectComparison", "LandAndBuilding"))
        ], multiGroup: true);

        p.Should().HaveCount(3);
        p[0].Should().Be("กลุ่มที่ 1 ผู้ประเมินใช้วิธีเปรียบเทียบตลาด (Market Approach) ในการสรุปมูลค่า โดยประเมินมูลค่าที่ดินพร้อมสิ่งปลูกสร้างด้วยวิธีเปรียบเทียบตลาดโดยตรง (Direct Comparison)");
        p[1].Should().StartWith("กลุ่มที่ 2 ผู้ประเมินใช้วิธีต้นทุน (Cost Approach)").And.Contain("ที่ดินด้วยวิธีเปรียบเทียบตลาดแบบปรับปรุงราคา (Sale Adjustment Grid)");
        // the reference sentence sits right under its own group's paragraph 1, with no group prefix
        p[2].Should().Be("ทั้งนี้ ได้ใช้วิธีเปรียบเทียบตลาด (Market Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา");
        p.Should().NotContain(x => x.Contains("ในการประเมินมูลค่าครั้งนี้")).And.NotContain(x => x.StartsWith("กลุ่มที่ 2 ทั้งนี้"));
    }

    [Fact]
    public void In_a_multi_group_appraisal_a_group_with_only_reference_approaches_keeps_its_group_lead()
    {
        var p = PricingNarrative.Build(
        [
            Row("Market", group: 1, methods: ("WQS", null)),
            Row("Income", reference: true, group: 1),
            Row("Cost", reference: true, group: 2)
        ], multiGroup: true);

        p.Should().Equal(
            "กลุ่มที่ 1 ผู้ประเมินใช้วิธีเปรียบเทียบตลาด (Market Approach) ในการสรุปมูลค่า โดยประเมินมูลค่าทรัพย์สินด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS)",
            "ทั้งนี้ ได้ใช้วิธีรายได้ (Income Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา",
            "กลุ่มที่ 2 ผู้ประเมินใช้วิธีต้นทุน (Cost Approach) เป็นวิธีอ้างอิงประกอบการพิจารณา");
    }

    [Theory]
    [InlineData("Income", "Income", null, "ประเมินมูลค่าทรัพย์สินด้วยวิธีวิเคราะห์รายได้ (Income)")]
    [InlineData("Residual", "Hypothesis", null, "ประเมินมูลค่าทรัพย์สินด้วยวิธีสมมติฐานการพัฒนา (Hypothesis)")]
    [InlineData("Cost", "ProfitRent", "Land", "ประเมินมูลค่าที่ดินด้วยวิธีกำไรส่วนเกินจากค่าเช่า (Profit Rent)")]
    [InlineData("Cost", "Leasehold", "Land", "ประเมินมูลค่าที่ดินด้วยวิธีประเมินมูลค่าสิทธิการเช่า (Leasehold)")]
    [InlineData("Cost", "MachineryCost", "Machinery", "ประเมินมูลค่าเครื่องจักรด้วยวิธีต้นทุนทดแทนหักค่าเสื่อมราคา")]
    [InlineData("Market", "SaleGrid", "LandAndBuilding", "ประเมินมูลค่าที่ดินพร้อมสิ่งปลูกสร้างด้วยวิธีเปรียบเทียบตลาดแบบปรับปรุงราคา (Sale Adjustment Grid)")]
    [InlineData("Other", "SomethingNew", null, "ประเมินมูลค่าทรัพย์สินด้วยSomethingNew")]   // an unknown type prints as it is, never crashes
    public void Every_method_type_the_picker_can_emit_has_a_phrase(string approach, string method, string? role, string expectedPart)
    {
        var p = PricingNarrative.Build([Row(approach, methods: (method, role))], false);

        p.Should().ContainSingle().Which.Should().Contain(expectedPart);
    }

    [Fact]
    public void A_used_approach_with_no_known_method_is_named_without_a_by_clause_and_no_rows_give_no_paragraph()
    {
        PricingNarrative.Build([Row("Market")], false).Should().Equal("ในการประเมินมูลค่าครั้งนี้ ผู้ประเมินใช้วิธีเปรียบเทียบตลาด (Market Approach) ในการสรุปมูลค่า");
        PricingNarrative.Build([], false).Should().BeEmpty();
    }

    // ── Section 6 approach intro ──────────────────────────────────────────────────────────

    [Fact]
    public void The_cost_intro_with_land_and_building_is_prose_with_the_land_method_then_the_depreciated_building_cost()
    {
        var a = Row("Cost", methods: [("BuildingCost", "Building"), ("WQS", "Land")]);

        PricingNarrative.IntroTitle(a).Should().Be("การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach)");
        PricingNarrative.IntroText(a).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach) เป็นการพิจารณามูลค่าตลาดของที่ดินด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS) " +
            "รวมกับต้นทุนค่าก่อสร้างตามสภาพปัจจุบันของสิ่งปลูกสร้างหลังหักค่าเสื่อมราคา โดยมีรายละเอียดดังนี้");
    }

    [Fact]
    public void A_cost_intro_with_one_part_keeps_only_the_matching_half()
    {
        PricingNarrative.IntroText(Row("Cost", methods: ("BuildingCost", "Building"))).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach) เป็นการพิจารณาต้นทุนค่าก่อสร้างตามสภาพปัจจุบันของสิ่งปลูกสร้างหลังหักค่าเสื่อมราคา โดยมีรายละเอียดดังนี้");
        PricingNarrative.IntroText(Row("Cost", methods: ("SaleGrid", "Land"))).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach) เป็นการพิจารณามูลค่าตลาดของที่ดินด้วยวิธีเปรียบเทียบตลาดแบบปรับปรุงราคา (Sale Adjustment Grid) โดยมีรายละเอียดดังนี้");
    }

    [Fact]
    public void The_market_intro_says_what_it_compares_with_and_joins_several_methods_the_usual_way()
    {
        PricingNarrative.IntroText(Row("Market", methods: ("WQS", null))).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีเปรียบเทียบตลาด (Market Approach) เป็นการเปรียบเทียบกับข้อมูลซื้อขายหรือเสนอขายทรัพย์สินที่คล้ายคลึงกัน ด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS) โดยมีรายละเอียดดังนี้");
        PricingNarrative.IntroText(Row("Market", methods: [("WQS", "Land"), ("SaleGrid", "Land"), ("WQS", "LandAndBuilding")])).Should().Contain(
            "ด้วยวิธีเปรียบเทียบตลาดแบบถ่วงน้ำหนักคะแนน (WQS) และวิธีเปรียบเทียบตลาดแบบปรับปรุงราคา (Sale Adjustment Grid) โดยมีรายละเอียดดังนี้");
    }

    [Fact]
    public void The_income_and_other_intros_name_their_methods_and_no_methods_cuts_the_with_clause()
    {
        PricingNarrative.IntroText(Row("Income", methods: ("Income", null))).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีรายได้ (Income Approach) ด้วยวิธีวิเคราะห์รายได้ (Income) โดยมีรายละเอียดดังนี้");
        PricingNarrative.IntroText(Row("Residual", methods: ("Hypothesis", null))).Should().Be(
            "การประเมินมูลค่าทรัพย์สินโดยวิธีสมมติฐาน (Residual Approach) ด้วยวิธีสมมติฐานการพัฒนา (Hypothesis) โดยมีรายละเอียดดังนี้");
        // a cost approach with machinery is not a land + building one: it names its method like the others
        PricingNarrative.IntroText(Row("Cost", methods: ("MachineryCost", "Machinery"))).Should().Contain("ด้วยวิธีต้นทุนทดแทนหักค่าเสื่อมราคา โดยมีรายละเอียดดังนี้");
        // no method recorded
        PricingNarrative.IntroText(Row("Income")).Should().Be("การประเมินมูลค่าทรัพย์สินโดยวิธีรายได้ (Income Approach) โดยมีรายละเอียดดังนี้");
        PricingNarrative.IntroText(Row("Market")).Should().Be("การประเมินมูลค่าทรัพย์สินโดยวิธีเปรียบเทียบตลาด (Market Approach) เป็นการเปรียบเทียบกับข้อมูลซื้อขายหรือเสนอขายทรัพย์สินที่คล้ายคลึงกัน โดยมีรายละเอียดดังนี้");
        PricingNarrative.IntroText(Row("Cost")).Should().Be("การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach) โดยมีรายละเอียดดังนี้");
    }

    [Fact]
    public async Task Section_6_prints_a_bold_title_and_the_intro_paragraph_for_each_used_approach_and_nothing_in_the_reference_pass()
    {
        const string template = "{{ include 'partials/book-funcs' }}{{ grp = model.property_groups[0]; multi6 = false; n6 = 0; any6 = false; h6 = '6.' }}{{ ref_pass = false }}{{ include 'partials/book-flow-methods' }}|REF|{{ ref_pass = true }}{{ include 'partials/book-flow-methods' }}";
        var model = new AppraisalSummaryModel
        {
            PropertyGroups = [new Reporting.Application.Models.Sections.PropertyGroupDetail { GroupNumber = 1 }],
            ApproachValues = [Row("Cost", methods: [("WQS", "Land"), ("BuildingCost", "Building")]), Row("Market", reference: true, methods: ("WQS", null))]
        };

        var html = await new Reporting.Infrastructure.Rendering.ScribanTemplateRenderer(NullLogger<Reporting.Infrastructure.Rendering.ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);
        var main = html[..html.IndexOf("|REF|", StringComparison.Ordinal)];
        var reference = html[html.IndexOf("|REF|", StringComparison.Ordinal)..];

        main.Should().Contain("<p class=\"bk-p\"><b>การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach)</b></p>")
            .And.Contain("<p class=\"bk-p\">การประเมินมูลค่าทรัพย์สินโดยวิธีต้นทุน (Cost Approach) เป็นการพิจารณามูลค่าตลาดของที่ดินด้วย")
            .And.NotContain("มูลค่าตลาด = ").And.NotContain("Market Approach");   // the reference Market approach has no intro
        reference.Should().NotContain("การประเมินมูลค่าทรัพย์สินโดย");
    }

    // ── 5.2 assumptions ───────────────────────────────────────────────────────────────

    private static async Task<string> RenderTailAsync(AppraisalSummaryModel model) =>
        await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-tail' }}", model, CancellationToken.None);

    private static string Section52(string html)
    {
        var s = html.IndexOf("<span>5.2</span>", StringComparison.Ordinal);
        return html[s..html.IndexOf("<span>5.3</span>", s, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Assumptions_print_as_a_paragraph_that_keeps_the_line_breaks()
    {
        var html = await RenderTailAsync(new AppraisalSummaryModel { AdditionalAssumptions = "1. ราคานี้ไม่รวมภาษี\n2. ที่ดินติดถนนสาธารณะ" });

        Section52(html).Should().Contain("<p class=\"bk-p\" style=\"white-space:pre-line;overflow-wrap:anywhere;\">1. ราคานี้ไม่รวมภาษี\n2. ที่ดินติดถนนสาธารณะ</p>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_assumptions_print_a_dash(string? text)
    {
        var html = await RenderTailAsync(new AppraisalSummaryModel { AdditionalAssumptions = text });

        Section52(html).Should().Contain(">-</p>");
    }
}
