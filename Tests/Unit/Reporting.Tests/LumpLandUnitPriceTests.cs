using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>
/// A land price recorded as a lump (ราคาเหมา: PerUnit or PerSqm) has no rate, so its ราคาต่อหน่วย cell
/// prints blank — never LandValue / area — while พื้นที่ and ราคาประเมิน still print. Only PerSqWa (or a
/// legacy NULL unit on the Cost row) prints a rate. The row-building sits on Dapper + SQL, so the pure
/// decisions are pinned directly and the blank cell through the real template.
/// </summary>
public class LumpLandUnitPriceTests
{
    // Appraisal 69000150: 5,000,000 on 913.33 sq.wa.
    private const decimal Area = 913.33m;
    private const decimal Lump = 5_000_000m;

    [Theory]
    [InlineData("PerUnit")]
    [InlineData("PerSqm")]
    public void Market_row_of_a_lump_keeps_area_and_value_but_has_no_rate(string unitType)
    {
        var (show, unitPrice, value) = AppraisalSummaryLandBuildingDataProvider.MarketLandColumns(
            unitType, true, "Land", true, landUnitPrice: Lump, valuePerUnit: Lump, landValue: Lump, area: Area);

        show.Should().BeTrue();
        unitPrice.Should().BeNull();
        value.Should().Be(Lump);
    }

    [Fact]
    public void Market_row_of_a_lump_without_a_land_value_is_not_shown()
    {
        var (show, _, value) = AppraisalSummaryLandBuildingDataProvider.MarketLandColumns(
            "PerUnit", true, "Land", true, null, null, landValue: null, area: Area);

        show.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Market_row_with_a_null_unit_shows_no_land_columns_even_with_a_land_value()
    {
        var (show, unitPrice, value) = AppraisalSummaryLandBuildingDataProvider.MarketLandColumns(
            null, true, "Land", true, landUnitPrice: Lump, valuePerUnit: Lump, landValue: Lump, area: Area);

        show.Should().BeFalse();
        unitPrice.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void Market_row_priced_per_sqwa_is_unchanged()
    {
        var (show, unitPrice, value) = AppraisalSummaryLandBuildingDataProvider.MarketLandColumns(
            "PerSqWa", true, "Land", true, landUnitPrice: null, valuePerUnit: 5_000m, landValue: 4_566_650m, area: Area);

        show.Should().BeTrue();
        unitPrice.Should().Be(5_000m);
        value.Should().Be(4_566_650m);   // 913.33 x 5,000
    }

    [Theory]
    [InlineData("PerUnit", true)]
    [InlineData("PerSqm", true)]
    [InlineData("PerSqWa", false)]
    [InlineData(null, false)]
    public void Only_an_explicit_non_land_rate_unit_is_a_lump(string? unitType, bool lump) =>
        AppraisalSummaryLandBuildingDataProvider.IsLumpUnit(unitType).Should().Be(lump);

    private static async Task<string> RenderAsync(SummaryGroupRow group)
    {
        var template = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Templates", "appraisal-summary-land-building.html"));
        return await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync(template, new AppraisalSummaryModel { Groups = [group] }, CancellationToken.None);
    }

    private static List<string> Cells(string html, string rowAnchor)
    {
        var rowStart = html.LastIndexOf("<tr", html.IndexOf(rowAnchor, StringComparison.Ordinal), StringComparison.Ordinal);
        var row = html[rowStart..html.IndexOf("</tr>", rowStart, StringComparison.Ordinal)];
        return System.Text.RegularExpressions.Regex.Matches(row, "<td[^>]*>(.*?)</td>", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Trim())
            .ToList();
    }

    [Fact]
    public async Task Market_row_of_a_lump_renders_a_blank_rate_cell_between_area_and_value()
    {
        var (show, unitPrice, value) = AppraisalSummaryLandBuildingDataProvider.MarketLandColumns(
            "PerUnit", true, "Land", true, Lump, Lump, Lump, Area);
        var html = await RenderAsync(new SummaryGroupRow
        {
            GroupNumber = 1, PropertyType = "ที่ดิน",
            ShowLandUnitColumns = show, MarketLandArea = Area, MarketLandUnitPrice = unitPrice, MarketLandValue = value
        });

        Cells(html, "<strong>ที่ดิน</strong>").TakeLast(3).Should().Equal("913.33", "", "5,000,000.00");
    }

    [Fact]
    public async Task Cost_split_land_row_of_a_lump_renders_a_blank_rate_cell_between_area_and_value()
    {
        var html = await RenderAsync(new SummaryGroupRow
        {
            GroupNumber = 1, SplitLandAndBuilding = true, HasLand = true, LandRowLabel = "ที่ดิน",
            TotalSquareWa = Area, LandUnitPrice = null, LandValue = Lump
        });

        Cells(html, "<strong>ที่ดิน</strong>").TakeLast(3).Should().Equal("913.33", "", "5,000,000.00");
    }

    [Fact]
    public async Task Cost_split_land_row_priced_per_sqwa_still_prints_its_rate()
    {
        var html = await RenderAsync(new SummaryGroupRow
        {
            GroupNumber = 1, SplitLandAndBuilding = true, HasLand = true, LandRowLabel = "ที่ดิน",
            TotalSquareWa = Area, LandUnitPrice = 5_000m, LandValue = 4_566_650m
        });

        Cells(html, "<strong>ที่ดิน</strong>").TakeLast(3).Should().Equal("913.33", "5,000.00", "4,566,650.00");
    }
}
