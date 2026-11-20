using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Book 3.2: the last column "ผู้ถือกรรมสิทธิ์" of the building table, from BuildingSection.OwnerText (the one rule, also read by the building page).</summary>
public class BookBuildingOwnerColumnTests
{
    private static async Task<string> RenderAsync(params BuildingSection[] buildings)
        => await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-flow-standard' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Lands = [new LandSection()], Buildings = buildings }] }, CancellationToken.None);

    private static string Table(string html)
    {
        var start = html.LastIndexOf("<table class=\"vt\">", html.IndexOf("บ้านเลขที่</th>", StringComparison.Ordinal), StringComparison.Ordinal);
        return html[start..html.IndexOf("</table>", start, StringComparison.Ordinal)];
    }

    [Theory]
    [InlineData("  นายตัวอย่าง ใจดี ", true, "นายตัวอย่าง ใจดี")]   // verified, trimmed
    [InlineData("นายตัวอย่าง", null, "นายตัวอย่าง")]            // unanswered reads as stated
    [InlineData("นายตัวอย่าง", false, "ไม่สามารถตรวจสอบได้")]         // not verified wins over the name
    [InlineData(null, false, "ไม่สามารถตรวจสอบได้")]
    [InlineData(null, true, "-")]
    [InlineData("   ", null, "-")]
    [InlineData("", true, "-")]
    public void Owner_text_is_the_one_rule(string? name, bool? verified, string expected)
        => new BuildingSection { OwnerName = name, IsOwnerVerified = verified }.OwnerText.Should().Be(expected);

    [Fact]
    public async Task The_table_has_a_last_owner_column_with_a_cell_per_building_and_a_dash_on_development_rows()
    {
        var html = await RenderAsync(
            new BuildingSection { HouseNumber = "10/1", UsableArea = 100m, OwnerName = " นายตัวอย่าง ใจดี ", IsOwnerVerified = true,
                DevelopmentItems = [new DevelopmentItem { BuildingTypeText = "รั้ว", Area = 12m }] },
            new BuildingSection { HouseNumber = "10/2", UsableArea = 50m, OwnerName = "ไม่ควรแสดง", IsOwnerVerified = false },
            new BuildingSection { HouseNumber = "10/3", UsableArea = 20m, OwnerName = "  " });

        var table = Table(html);

        table.Should().Contain("โฉนดเลขที่</th>").And.Contain(">ผู้ถือกรรมสิทธิ์</th>");
        table.IndexOf(">ผู้ถือกรรมสิทธิ์</th>", StringComparison.Ordinal).Should().BeGreaterThan(table.IndexOf("โฉนดเลขที่</th>", StringComparison.Ordinal));
        table.Should().Contain("<td style=\"overflow-wrap:anywhere;\">นายตัวอย่าง ใจดี</td>")
            .And.Contain("<td style=\"overflow-wrap:anywhere;\">ไม่สามารถตรวจสอบได้</td>")
            .And.Contain("<td style=\"overflow-wrap:anywhere;\">-</td>")
            .And.NotContain("ไม่ควรแสดง");
        // the development row prints a dash in the owner column
        table.Should().Contain("<td class=\"r\">12.00</td><td></td><td>-</td></tr>");
        // every row of the table has as many cells as it has columns (header 7: no license column), the total row included
        System.Text.RegularExpressions.Regex.Matches(table, @"<tr[^>]*>.*?</tr>", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => System.Text.RegularExpressions.Regex.Matches(m.Value, @"<t[dh]\b[^>]*>").Select(x => x.Value.Contains("colspan=\"3\"") ? 3 : x.Value.Contains("colspan=\"6\"") ? 6 : 1).Sum())
            .Should().OnlyContain(n => n == 7 || n == 6 || n == 8, "borders stay intact");
    }

    [Fact]
    public async Task The_building_page_of_the_construction_book_reads_the_same_rule()
    {
        // the land book's page skips these rows (the 3.2 table above carries them); the construction book keeps them
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(
            "{{ include 'partials/book-funcs' }}{{ b = model.all_buildings[0]; bld_k = 1 }}{{ include 'partials/book-building-block' }}",
            new AppraisalSummaryModel { PropertyGroups = [new PropertyGroupDetail { Buildings = [new BuildingSection { HouseNumber = "10/1", OwnerName = "นายตัวอย่าง", IsOwnerVerified = false }] }] },
            CancellationToken.None);

        html.Should().Contain("<span class=\"l\">ผู้ถือกรรมสิทธิ์อาคาร</span><span class=\"c\">:</span><span class=\"v\">ไม่สามารถตรวจสอบได้</span>")
            .And.NotContain("นายตัวอย่าง");   // the stored name is hidden behind the fixed phrase
    }
}
