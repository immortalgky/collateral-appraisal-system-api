using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Summary attributes block: ภาระผูกพัน label, ผังเมือง before ภาระผูกพันอื่นๆ, the others row hidden when "ไม่มี", colour-first ผังเมือง.</summary>
public class SummaryAttributesBlockTests
{
    private static async Task<string> RenderAsync(string template, AppraisalSummaryModel model)
    {
        var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Templates", template));
        return await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(html, model, CancellationToken.None);
    }

    private static AppraisalSummaryModel Model(string others) => new()
    {
        ObligationMortgage = "จำนองเป็นประกัน",
        ObligationOthers = others,
        CityPlan = AppraisalSummaryCommonLoader.CityPlan("สีน้ำตาล", "ย่านที่อยู่อาศัยหนาแน่นมาก")
    };

    [Theory]
    [InlineData("appraisal-summary-land-building.html")]
    [InlineData("appraisal-summary-condo.html")]
    public async Task Mortgage_label_is_shortened_and_the_others_row_is_shown_when_present(string template)
    {
        var html = await RenderAsync(template, Model("ติดสัญญาเช่า"));

        html.Should().Contain("<span class=\"k\">ภาระผูกพัน</span>").And.NotContain("ภาระผูกพันการจำนอง")
            .And.MatchRegex("<span class=\"k\">ภาระผูกพันอื่นๆ</span><span class=\"v\"( style=\"white-space:pre-line\")?>ติดสัญญาเช่า</span>");
    }

    [Theory]
    [InlineData("appraisal-summary-land-building.html")]
    [InlineData("appraisal-summary-condo.html")]
    public async Task The_others_row_is_hidden_when_there_are_none(string template)
    {
        var html = await RenderAsync(template, Model(ObligationText.None));

        html.Should().NotContain("ภาระผูกพันอื่นๆ").And.Contain("จำนองเป็นประกัน");
    }

    [Fact]
    public void City_plan_is_color_first_and_skips_blanks()
    {
        AppraisalSummaryCommonLoader.CityPlan("สีน้ำตาล", "ย่านที่อยู่อาศัยหนาแน่นมาก").Should().Be("สีน้ำตาล · ย่านที่อยู่อาศัยหนาแน่นมาก");
        AppraisalSummaryCommonLoader.CityPlan(null, "ย่านที่อยู่อาศัยหนาแน่นมาก").Should().Be("ย่านที่อยู่อาศัยหนาแน่นมาก");
        AppraisalSummaryCommonLoader.CityPlan(" ", null).Should().BeNull();
    }

    private static readonly string[] StandardOrder =
    [
        "กรรมสิทธิ์ที่ดิน", "สิทธิทางเข้า-ออก", "กรรมสิทธิ์สิ่งปลูกสร้าง", "สภาพที่ดิน", "ภาระผูกพัน", "ผังเมือง",
        "ภาระผูกพันอื่นๆ", "พิกัด", "ราคาประเมินราชการ", "สภาพการใช้ประโยชน์"
    ];

    private static readonly string[] CondoOrder =
    [
        "กรรมสิทธิ์ห้องชุด", "สิทธิทางเข้า-ออก", "ภาระผูกพัน", "สภาพที่ดิน", "ภาระผูกพันอื่นๆ",
        "พิกัด", "ผังเมือง", "ราคาประเมินราชการ", "สภาพการใช้ประโยชน์"
    ];

    private static List<int> LabelPositions(string html, string[] order) =>
        order.Select(l => html.IndexOf($"<span class=\"k\">{l}</span>", StringComparison.Ordinal)).ToList();

    [Theory]
    [InlineData("appraisal-summary-land-building.html", false)]
    [InlineData("appraisal-summary-condo.html", true)]
    public async Task Others_is_a_full_row_between_the_grids_in_the_documented_order(string template, bool condo)
    {
        var html = await RenderAsync(template, Model("ติดสัญญาเช่า"));

        LabelPositions(html, condo ? CondoOrder : StandardOrder).Should().OnlyContain(p => p >= 0).And.BeInAscendingOrder();
        // a full row: it sits between two blocks, not inside the cols2 grid
        System.Text.RegularExpressions.Regex.IsMatch(html,
            @"</div>\s*</div>\s*<div class=""kv""><span class=""k"">ภาระผูกพันอื่นๆ</span><span class=""v""( style=""white-space:pre-line"")?>ติดสัญญาเช่า</span></div>\s*<div class=""(cols2|kv)""")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("appraisal-summary-land-building.html", false)]
    [InlineData("appraisal-summary-condo.html", true)]
    public async Task Hiding_the_others_row_leaves_every_other_cell_in_place(string template, bool condo)
    {
        var order = condo ? CondoOrder : StandardOrder;
        var withOthers = await RenderAsync(template, Model("ติดสัญญาเช่า"));
        var without = await RenderAsync(template, Model(ObligationText.None));

        var positions = LabelPositions(without, order);
        positions[order.ToList().IndexOf("ภาระผูกพันอื่นๆ")].Should().Be(-1);
        positions.Where(p => p >= 0).Should().HaveCount(order.Length - 1).And.BeInAscendingOrder();
        // same sequence of labels with and without the row, minus the row itself
        System.Text.RegularExpressions.Regex.Matches(without, @"<span class=""k"">([^<]+)</span>").Select(m => m.Groups[1].Value)
            .Should().Equal(System.Text.RegularExpressions.Regex.Matches(withOthers, @"<span class=""k"">([^<]+)</span>").Select(m => m.Groups[1].Value).Where(l => l != "ภาระผูกพันอื่นๆ"));
    }
}
