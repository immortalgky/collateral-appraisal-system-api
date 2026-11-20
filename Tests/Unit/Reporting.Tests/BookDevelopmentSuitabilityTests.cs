using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>ความเหมาะสมต่อการพัฒนา (book 1.2) sits under ความเจริญของพื้นที่; the LandShape dropdown is labelled รูปแปลงที่ดิน.</summary>
public class BookDevelopmentSuitabilityTests
{
    private static Task<string> RenderAsync(string template, LandSection land) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            template, new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [land] }] },
            CancellationToken.None);

    private const string Book =
        "{{ include 'partials/book-funcs' }}{{ multi_land = false }}{{ include 'partials/book-land-location' }}";

    [Fact]
    public async Task Book_prints_the_suitability_row_right_under_area_growth()
    {
        var html = await RenderAsync(Book, new LandSection
        {
            PropertyAnticipationType = "เจริญปานกลาง",
            DevelopmentSuitability = "รูปแปลงและขนาดเหมาะสมต่อการพัฒนา ให้ประโยชน์ได้มาก"
        });

        var growth = html.IndexOf("ความเจริญของพื้นที่", StringComparison.Ordinal);
        var suitability = html.IndexOf("ความเหมาะสมต่อการพัฒนา", StringComparison.Ordinal);
        growth.Should().BeGreaterThan(-1);
        suitability.Should().BeGreaterThan(growth);
        html.Should().Contain("รูปแปลงและขนาดเหมาะสมต่อการพัฒนา ให้ประโยชน์ได้มาก");
    }

    [Fact]
    public async Task Book_prints_a_plain_dash_when_suitability_is_empty()
    {
        var html = await RenderAsync(Book, new LandSection());

        html.Should().Contain("ความเหมาะสมต่อการพัฒนา</span><span class=\"c\">:</span><span class=\"v\">-</span>")
            .And.NotContain("l miss\">ความเหมาะสมต่อการพัฒนา");
    }

    [Fact]
    public async Task Land_summary_labels_the_shape_dropdown_as_land_shape()
    {
        var html = await RenderAsync("{{ land_section = model.all_lands[0] }}{{ include 'partials/section-land' }}",
            new LandSection { LandDescription = "สี่เหลี่ยมผืนผ้า" });

        html.Should().Contain("รูปแปลงที่ดิน :</b> สี่เหลี่ยมผืนผ้า").And.NotContain("ลักษณะทางกายภาพ :</b> สี่เหลี่ยมผืนผ้า");
    }

    [Fact]
    public async Task Book_prints_one_city_plan_row_with_the_colour_then_the_type_and_hides_it_when_empty()
    {
        var with = await RenderAsync(Book, new LandSection { UrbanPlanningType = "ย่านพานิชยกรรม", UrbanPlanningColor = "สีแดง" });
        var without = await RenderAsync(Book, new LandSection());

        with.Should().Contain("<span class=\"l\">ผังเมือง</span><span class=\"c\">:</span><span class=\"v\">สีแดง ย่านพานิชยกรรม</span>")
            .And.NotContain("สีผังเมือง</span>");
        without.Should().NotContain("<span class=\"l\">ผังเมือง</span>");
    }

    [Fact]
    public async Task Standard_and_condo_books_fill_the_color_zone_row_instead_of_a_red_dash()
    {
        var land = await RenderAsync("{{ include 'partials/book-flow-standard' }}", new LandSection { UrbanPlanningColor = "สีเขียว" });
        var condo = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-condo' }}",
            new AppraisalSummaryModel { CondoSection = new CondoSection { UrbanPlanningColor = "สีส้ม" } }, CancellationToken.None);

        land.Should().Contain("เป็นพื้นที่สี</span><span class=\"c\">:</span><span class=\"v\">สีเขียว</span>")
            .And.NotContain("สีผังเมือง</span>");
        condo.Should().Contain("เป็นพื้นที่สี</span><span class=\"c\">:</span><span class=\"v\">สีส้ม</span>")
            .And.NotContain("l miss\">เป็นพื้นที่สี");
    }
}
