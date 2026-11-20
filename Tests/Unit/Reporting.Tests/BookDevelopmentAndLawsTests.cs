using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book development page (item title = building type, area in ตร.ม.) and the law section (images above the remark, centred).</summary>
public class BookDevelopmentAndLawsTests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, model, CancellationToken.None);

    private static Task<string> RenderDevAsync(DevelopmentItem item) =>
        RenderAsync("{{ include 'partials/book-funcs' }}{{ dev_items = model.all_development_items; dev_photos = []; include 'partials/book-development' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [new BuildingSection { DevelopmentItems = [item] }] }] });

    [Fact]
    public async Task Development_item_title_is_the_building_type_and_the_area_has_its_unit()
    {
        var html = await RenderDevAsync(new DevelopmentItem { BuildingTypeText = "บ้านเดี่ยว", AreaDescription = "เนื้อที่ภายใน", Area = 77.77m });

        html.Should().Contain("<div class=\"vk-item\">บ้านเดี่ยว</div>").And.NotContain(">เนื้อที่ภายใน<")
            .And.Contain("<span class=\"l\">พื้นที่</span>").And.NotContain("พื้นที่ / ความยาว")
            .And.Contain("ประมาณ 77.77").And.Contain("ตร.ม.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Development_item_title_falls_back_to_the_description_when_the_type_is_blank(string? type)
    {
        var html = await RenderDevAsync(new DevelopmentItem { BuildingTypeText = type, AreaDescription = "รั้วคอนกรีต" });

        html.Should().Contain("<div class=\"vk-item\">รั้วคอนกรีต</div>");
        html.Should().NotContain("ตร.ม."); // no area, so no unit
    }

    private static LawRecord Law(int images, string? remark = "ข้อความตัวอย่าง") => new()
    {
        Topic = "พ.ร.บ.ตัวอย่าง", Remark = remark,
        Images = Enumerable.Range(1, images).Select(i => new AppendixImage { ImgSrc = $"file:///x/{i}.png", Caption = $"รูป {i}" }).ToList()
    };

    private static Task<string> RenderLawsAsync(params LawRecord[] laws) =>
        RenderAsync("{{ include 'partials/book-laws' }}", new AppraisalSummaryModel { LawRecords = laws });

    [Fact]
    public async Task Law_images_come_above_the_remark_and_are_centred()
    {
        var html = await RenderLawsAsync(Law(1));

        html.IndexOf("<img", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("ข้อความตัวอย่าง", StringComparison.Ordinal))
            .And.BeGreaterThan(html.IndexOf("พ.ร.บ.ตัวอย่าง", StringComparison.Ordinal));
        html.Should().Contain(".law-grid .cell { text-align:center; }")
            .And.Contain(".law-grid .cell:only-child, .law-grid .cell:last-child:nth-child(odd) { grid-column:1 / -1; }")
            .And.NotContain("text-align:left");
    }

    [Fact]
    public async Task A_record_up_to_one_row_is_kept_whole_and_a_longer_one_flows_with_the_heading_kept_on_its_first_row()
    {
        var two = await RenderLawsAsync(Law(2));
        var four = await RenderLawsAsync(Law(4));

        two.Should().Contain("<div class=\"bk-keep\">\n<div class=\"bk-h2\"");
        four.Should().NotContain("<div class=\"bk-keep\">\n<div class=\"bk-h2\"").And.Contain("break-after:avoid");
        four.Split("<div class=\"cell\">").Length.Should().Be(5); // 4 images, 2 per row
    }

    [Fact]
    public async Task A_record_without_images_still_prints_its_remark()
    {
        var html = await RenderLawsAsync(Law(0));

        html.Should().Contain("ข้อความตัวอย่าง").And.NotContain("law-grid\">");
    }
}
