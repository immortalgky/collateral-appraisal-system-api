using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>เลขที่ใบอนุญาตก่อสร้าง: the standard book, the construction book and the building summary print the stored number; empty is a plain "-".</summary>
public class BookLicenseNumberTests
{
    private static Task<string> RenderAsync(string template, AppraisalSummaryModel model) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);

    private static AppraisalSummaryModel Standard(BuildingSection building) => new()
    {
        PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings = [building] }]
    };

    [Fact]
    public async Task Standard_book_prints_the_number_and_keeps_the_expiry_suffix()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-standard' }}",
            Standard(new BuildingSection { LicenseNumber = "ก-123/2566", LicenseExpiration = new DateTime(2027, 5, 1) }));

        html.Should().Contain("ใบอนุญาตก่อสร้าง ก-123/2566 (หมดอายุ");
    }

    private const string LicenseHeader = "หนังสือแสดง<br/>กรรมสิทธิ์</th>";

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Standard_book_drops_the_license_column_when_no_building_has_a_number(string? number)
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-standard' }}",
            Standard(new BuildingSection { LicenseNumber = number }));

        html.Should().NotContain(LicenseHeader).And.NotContain("ใบอนุญาตก่อสร้าง");
        // the total row loses its extra empty cell with the column: 3 label cells + area + title cell + owner cell
        html.Should().Contain("<td colspan=\"3\" class=\"r\">รวมสิ่งปลูกสร้าง ทรัพย์สินที่ประเมินมูลค่า</td><td class=\"r\">0.00</td><td></td><td></td></tr>");
    }

    [Fact]
    public async Task Standard_book_keeps_the_column_when_one_building_has_a_number_and_leaves_the_others_blank()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-standard' }}", new AppraisalSummaryModel
        {
            PropertyGroups =
            [
                new PropertyGroupDetail
                {
                    Lands = [new LandSection()],
                    Buildings =
                    [
                        new BuildingSection { HouseNumber = "10/1", LicenseNumber = "ก-123/2566" },
                        new BuildingSection { HouseNumber = "10/2" }
                    ]
                }
            ]
        });

        html.Should().Contain(LicenseHeader)
            .And.Contain("<td>ใบอนุญาตก่อสร้าง ก-123/2566</td>")
            .And.NotContain("ใบอนุญาตก่อสร้าง -");
        // the unlicensed row ends in an empty license cell
        System.Text.RegularExpressions.Regex.IsMatch(html, @"<td>10/2</td>\s*<td>-</td>\s*<td class=""r"">-</td>\s*<td>-</td>\s*<td></td>\s*<td style=""overflow-wrap:anywhere;"">-</td>\s*</tr>").Should().BeTrue();
        html.Split("ใบอนุญาตก่อสร้าง").Length.Should().Be(2); // printed once
    }

    [Fact]
    public async Task Construction_book_prints_the_number_and_a_plain_dash_when_empty()
    {
        var with = await RenderAsync("{{ include 'partials/book-flow-construction' }}", new AppraisalSummaryModel
        {
            ConstructionSection = new ConstructionSection { Buildings = [new ConstructionBuilding { LicenseNumber = "ก-123/2566" }] }
        });
        var without = await RenderAsync("{{ include 'partials/book-flow-construction' }}", new AppraisalSummaryModel
        {
            ConstructionSection = new ConstructionSection { Buildings = [new ConstructionBuilding()] }
        });

        with.Should().Contain("เลขที่ใบอนุญาตก่อสร้าง</span><span class=\"c\">:</span><span class=\"v\">ก-123/2566");
        without.Should().Contain("เลขที่ใบอนุญาตก่อสร้าง</span><span class=\"c\">:</span><span class=\"v\">-</span>")
            .And.NotContain("l miss\">เลขที่ใบอนุญาตก่อสร้าง");
    }

    [Fact]
    public async Task Building_summary_prints_the_number()
    {
        var html = await RenderAsync("{{ building_section = model.all_buildings[0] }}{{ include 'partials/section-building' }}",
            Standard(new BuildingSection { LicenseNumber = "ก-123/2566" }));

        html.Should().Contain("ใบอนุญาตเลขที่ :</b> ก-123/2566");
    }

    [Fact]
    public async Task Construction_book_renders_with_dashes_when_the_appraisal_has_no_inspection_data()
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-construction' }}", new AppraisalSummaryModel());

        html.Should().Contain("รายงานการตรวจงวดก่อสร้างครั้งก่อน</span><span class=\"c\">:</span><span class=\"v\">-</span>");
    }
}
