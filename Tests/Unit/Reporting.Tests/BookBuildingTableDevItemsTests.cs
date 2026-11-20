using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book 3.2 building table: the group's ส่วนพัฒนาอื่นๆ rows follow the buildings, numbered on, outside the area total.</summary>
public class BookBuildingTableDevItemsTests
{
    private const string Intro = "จำนวน 1 รายการ และส่วนพัฒนาอื่นๆ บนที่ดิน ดังนี้";

    private static async Task<string> RenderAsync(string? license, params DevelopmentItem[] devItems)
    {
        var model = new AppraisalSummaryModel
        {
            PropertyGroups =
            [
                new PropertyGroupDetail
                {
                    Lands = [new LandSection()],
                    Buildings =
                    [
                        new BuildingSection
                        {
                            HouseNumber = "10/1", UsableArea = 100m, LicenseNumber = license,
                            DevelopmentItems = devItems.ToList()
                        }
                    ]
                }
            ]
        };
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync("{{ include 'partials/book-flow-standard' }}", model, CancellationToken.None);
        var start = html.LastIndexOf("<table class=\"vt\">", html.IndexOf("บ้านเลขที่</th>", StringComparison.Ordinal), StringComparison.Ordinal);
        return html[start..html.IndexOf("</table>", start, StringComparison.Ordinal)];
    }

    [Theory]
    [InlineData(null, 6)]
    [InlineData("ก-1/2566", 7)]
    public async Task Dev_items_follow_the_buildings_with_a_sub_heading_and_stay_out_of_the_total(string? license, int columns)
    {
        // the first item has its parent's building type (the title) and an area; the second falls back to its description, with no area
        var table = await RenderAsync(license,
            new DevelopmentItem { BuildingTypeText = "บ้านเดี่ยว", AreaDescription = "สระว่ายน้ำ", Area = 12.5m },
            new DevelopmentItem { BuildingTypeText = "  ", AreaDescription = "รั้ว", Area = null });

        table.Should().Contain($"<tr class=\"sub\"><td colspan=\"{columns}\">ส่วนพัฒนาอื่นๆ บนที่ดิน</td></tr>")
            .And.Contain("<td class=\"c\">2</td><td></td><td>บ้านเดี่ยว</td><td class=\"r\">12.50</td><td></td>")
            .And.Contain("<td class=\"c\">3</td><td></td><td>รั้ว</td><td class=\"r\"></td><td></td>")
            .And.NotContain("สระว่ายน้ำ");
        // the area total is the building's own and the total row stays last
        table.IndexOf("รวมสิ่งปลูกสร้าง ทรัพย์สินที่ประเมินมูลค่า", StringComparison.Ordinal).Should().BeGreaterThan(table.IndexOf("รั้ว", StringComparison.Ordinal));
        table.Should().Contain("<td class=\"r\">100.00</td><td></td>").And.NotContain("112.50"); // the dev area (12.50) is not in รวมสิ่งปลูกสร้าง ทรัพย์สินที่ประเมินมูลค่า
        // every dev row has the same number of cells as the table has columns
        System.Text.RegularExpressions.Regex.Matches(table, @"<tr><td class=""c"">[23]</td>.*?</tr>", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => System.Text.RegularExpressions.Regex.Matches(m.Value, "<td").Count).Should().OnlyContain(c => c == columns);
    }

    [Fact]
    public async Task Without_dev_items_the_table_is_unchanged()
    {
        var table = await RenderAsync(null);

        table.Should().NotContain("ส่วนพัฒนาอื่นๆ").And.NotContain("class=\"sub\"");
    }

    [Fact]
    public async Task The_intro_names_the_dev_items_only_when_there_are_some()
    {
        var model = (string[] dev) => new AppraisalSummaryModel
        {
            PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings = [new BuildingSection { DevelopmentItems = dev.Select(d => new DevelopmentItem { AreaDescription = d }).ToList() }] }]
        };
        var renderer = new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance);
        var with = await renderer.RenderRawAsync("{{ include 'partials/book-flow-standard' }}", model(["รั้ว"]), CancellationToken.None);
        var without = await renderer.RenderRawAsync("{{ include 'partials/book-flow-standard' }}", model([]), CancellationToken.None);

        with.Should().Contain(Intro);
        without.Should().Contain("จำนวน 1 รายการ ดังนี้").And.NotContain("และส่วนพัฒนาอื่นๆ บนที่ดิน");
    }

    [Fact]
    public async Task Dev_rows_do_not_shift_the_building_block_numbering()
    {
        var model = new AppraisalSummaryModel
        {
            PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings = [new BuildingSection { HouseNumber = "10/1", DevelopmentItems = [new DevelopmentItem { AreaDescription = "รั้ว" }] }, new BuildingSection { HouseNumber = "10/2" }] }]
        };
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync("{{ include 'partials/book-flow-standard' }}", model, CancellationToken.None);

        // the blocks after the table are numbered by building (1, 2), not by building + dev rows
        html.Should().Contain("สิ่งปลูกสร้างรายการที่ 1").And.Contain("สิ่งปลูกสร้างรายการที่ 2").And.NotContain("สิ่งปลูกสร้างรายการที่ 3");
    }
}
