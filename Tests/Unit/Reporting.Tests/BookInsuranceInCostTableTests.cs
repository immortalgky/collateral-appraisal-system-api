using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>ทุนประกันภัยสิ่งปลูกสร้าง closes the last cost-building table; section 7 keeps its own box only when no such table printed.</summary>
public class BookInsuranceInCostTableTests
{
    private const string Label = "ทุนประกันภัยสิ่งปลูกสร้าง";
    private const string Valuation =
        "{{ include 'partials/book-funcs' }}{{ val_no = '6'; val_id = 's6'; val_title = 'การประเมินมูลค่า'; include 'partials/book-valuation' }}" +
        "{{ concl_no = '7.'; concl_id = 's7'; concl_machine = false; concl_construction = false; concl_block = false; include 'partials/book-conclusion' }}";

    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static BuildingSection Cost(bool reference = false) => new()
    {
        BuildingTypeText = "บ้านเดี่ยว", BuildingNumber = 1, CostIsReference = reference,
        CostRows = [new BuildingCostRow { AreaItemName = "บ้านเดี่ยว", Area = 80m, Price = 8_000_000m, ValueAfterDepreciation = 7_920_000m }],
        TotalValueAfterDepreciation = 7_920_000m, CostRowsTotal = 7_920_000m
    };

    private static AppraisalSummaryModel Model(decimal? cover, params (int Group, BuildingSection B)[] buildings) => new()
    {
        BuildingCoverageAmount = cover, ForcedSaleValue = 9_000_000m, TotalAppraisalValue = 10_000_000m,
        PropertyGroups = buildings.GroupBy(b => b.Group).Select(g => new PropertyGroupDetail { GroupNumber = g.Key, Buildings = g.Select(x => x.B).ToList() }).ToList(),
        Groups = buildings.Select(b => b.Group).Distinct().Select(n => new SummaryGroupRow { GroupNumber = n, BuildingSubtotal = 7_920_000m, BuildingSubtotalComputed = 7_920_000m }).ToList()
    };

    private static string Section7(string html) => html[html.IndexOf("สรุปการประเมินมูลค่าทรัพย์สิน</span>", StringComparison.Ordinal)..];

    private static string LastCostTable(string html)
    {
        var start = html.LastIndexOf("<table class=\"grid\">", StringComparison.Ordinal);
        return html[start..html.IndexOf("</table>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task With_a_cost_table_the_insured_sum_is_its_last_row_and_section_7_drops_the_box_and_Forced_Sale_takes_the_full_row()
    {
        var html = await RenderAsync(Valuation, Model(12_000_000m, (1, Cost())));

        var table = LastCostTable(html);
        table.Should().Contain($"<td colspan=\"8\">{Label}</td><td class=\"num\">12,000,000.00</td>");
        table.IndexOf(Label, StringComparison.Ordinal).Should().BeGreaterThan(table.IndexOf("ตามสภาพปัจจุบัน", StringComparison.Ordinal));
        table.LastIndexOf("<tr", StringComparison.Ordinal).Should().Be(table.IndexOf($"<tr style=\"font-weight:700;\"><td colspan=\"8\">{Label}", StringComparison.Ordinal)); // the last row of the table
        var s7 = Section7(html);
        s7.Should().NotContain(Label).And.Contain("(Forced Sale Value)");
    }

    [Fact]
    public async Task Without_a_cost_table_section_7_keeps_the_insurance_box()
    {
        var html = await RenderAsync(Valuation, Model(12_000_000m));

        Section7(html).Should().Contain(Label).And.Contain("12,000,000.- บาท").And.Contain("(Insurance Value)");
    }

    [Fact]
    public async Task Two_groups_with_cost_tables_print_the_book_wide_figure_once_after_the_last_table()
    {
        var html = await RenderAsync(Valuation, Model(12_000_000m, (1, Cost()), (2, Cost())));

        html.Split($"<td colspan=\"8\">{Label}</td>").Length.Should().Be(2);
        var first = html.IndexOf("<table class=\"grid\">", StringComparison.Ordinal);
        var last = html.LastIndexOf("<table class=\"grid\">", StringComparison.Ordinal);
        last.Should().BeGreaterThan(first);
        html.IndexOf($"<td colspan=\"8\">{Label}</td>", StringComparison.Ordinal).Should().BeGreaterThan(last);
        Section7(html).Should().NotContain(Label);
    }

    [Fact]
    public async Task A_reference_only_cost_table_does_not_carry_the_figure_and_section_7_keeps_it()
    {
        var html = await RenderAsync(Valuation, Model(12_000_000m, (1, Cost(reference: true))));

        html.Should().Contain("<table class=\"grid\">");
        html.Split($"<td colspan=\"8\">{Label}</td>").Length.Should().Be(1);
        Section7(html).Should().Contain(Label);
    }

    [Fact]
    public async Task With_no_insured_sum_the_cost_table_has_no_row_and_section_7_keeps_its_box()
    {
        var html = await RenderAsync(Valuation, Model(null, (1, Cost())));

        LastCostTable(html).Should().NotContain(Label);
        Section7(html).Should().Contain(Label);
    }

    [Fact]
    public async Task The_condo_book_has_no_cost_building_table_so_section_7_keeps_the_box()
    {
        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ include 'partials/book-flow-tail' }}",
            new AppraisalSummaryModel { BodyType = "condo", BuildingCoverageAmount = 3_000_000m, ForcedSaleValue = 2_000_000m, TotalAppraisalValue = 4_000_000m });

        html.Should().NotContain("<table class=\"grid\">");
        Section7(html).Should().Contain(Label).And.Contain("3,000,000.-");
    }
}
