using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// Renders the real appraisal-summary-condo.html through the app's Scriban renderer: a multi-unit
/// group prints one "- " line per unit, a single unit keeps the plain cell.
/// </summary>
public class CondoSummaryTemplateTests
{
    private static async Task<string> RenderAsync(AppraisalSummaryModel model)
    {
        var template = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Templates", "appraisal-summary-condo.html"));
        return await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, model, CancellationToken.None);
    }

    private static AppraisalSummaryModel Model(string location, string details, List<string> items) => new()
    {
        CollateralAddress = location,
        Groups =
        [
            new SummaryGroupRow
            {
                GroupNumber = 1, PropertyType = "ห้องชุด",
                CollateralDetails = details, DetailItems = items.ConvertAll(i => new SummaryItemRow { Description = i })
            }
        ]
    };

    private static string CollateralCell(string html)
    {
        var start = html.IndexOf("<strong>ห้องชุด</strong>", StringComparison.Ordinal);
        start = html.IndexOf("<td>", start, StringComparison.Ordinal);
        return html[start..(html.IndexOf("</td>", start, StringComparison.Ordinal) + 5)];
    }

    private static string LocationLine(string html)
    {
        var start = html.IndexOf("<span class=\"k\">ที่ตั้งทรัพย์สิน</span>", StringComparison.Ordinal);
        return html[start..(html.IndexOf("</span></div>", start, StringComparison.Ordinal) + 7)];
    }

    [Fact]
    public async Task Two_units_print_one_dash_line_each()
    {
        var rows = new[]
        {
            new AppraisalSummaryCommonLoader.CollateralLocationRow { Kind = "U", RoomNumber = "312/429", FloorNumber = "31", CondoName = "อาคารชุดโนเบิล", Province = "กรุงเทพมหานคร" },
            new AppraisalSummaryCommonLoader.CollateralLocationRow { Kind = "U", RoomNumber = "312/430", FloorNumber = "31", CondoName = "อาคารชุดโนเบิล", Province = "กรุงเทพมหานคร" }
        };
        var location = AppraisalSummaryCommonLoader.ComposeCollateralLocations(rows).Condo!.Address;
        var items = new List<string> { "ห้องชุดเลขที่ 312/429 ชั้นที่ 31", "ห้องชุดเลขที่ 312/430 ชั้นที่ 31" };

        var html = await RenderAsync(Model(location, string.Join(" | ", items), items));

        CollateralCell(html).Should().Be(
            "<td><div>- ห้องชุดเลขที่ 312/429 ชั้นที่ 31</div><div>- ห้องชุดเลขที่ 312/430 ชั้นที่ 31</div></td>");
        LocationLine(html).Should().Be(
            "<span class=\"k\">ที่ตั้งทรัพย์สิน</span><span class=\"v\">ห้องชุดเลขที่ 312/429, 312/430 ชั้นที่ 31 อาคารชุดโนเบิล จังหวัดกรุงเทพมหานคร</span>");
    }

    [Fact]
    public async Task One_unit_keeps_the_plain_cell()
    {
        var html = await RenderAsync(Model("ห้องชุดเลขที่ 312/429", "ห้องชุดเลขที่ 312/429 ชั้นที่ 31", ["ห้องชุดเลขที่ 312/429 ชั้นที่ 31"]));

        CollateralCell(html).Should().Be("<td>ห้องชุดเลขที่ 312/429 ชั้นที่ 31</td>").And.NotContain("- ");
    }
}
