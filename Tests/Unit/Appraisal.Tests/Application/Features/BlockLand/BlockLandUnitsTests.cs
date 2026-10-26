using Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;
using Appraisal.Application.Features.Project.UploadProjectUnits;
using Appraisal.Domain.Projects;
using Appraisal.Tests.Application.Features.ImportProjectUnitPrices;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.BlockLand;

/// <summary>Land (L) units: upload parsing and the re-match comparison ignore everything about a building.</summary>
public class BlockLandUnitsTests
{
    private static MemoryStream Sheet(string[] headers, params object?[][] rows) => PriceSheet.Build(headers, rows);

    [Fact]
    public void Parse_Land_ReadsPlotLandAreaAndPrice_AndIgnoresBuildingColumns()
    {
        // The LB-only columns are absent: a Land file must not need them.
        using var file = Sheet(
            ["Plot Number", "House Number", "Land Area", "Selling Price"],
            ["P1", "99/1", 50.5m, 1_000_000m],
            ["P2", null, 60m, 1_200_000m]);

        var units = ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid());

        Assert.Equal(2, units.Count);
        Assert.Equal("P1", units[0].PlotNumber);
        Assert.Equal("99/1", units[0].HouseNumber);
        Assert.Equal(50.5m, units[0].LandArea);
        Assert.Equal(1_000_000m, units[0].SellingPrice);
        Assert.Null(units[1].HouseNumber);
        Assert.All(units, u =>
        {
            Assert.Null(u.ModelType);
            Assert.Null(u.NumberOfFloors);
            Assert.Null(u.UsableArea);
        });
    }

    [Fact]
    public void Parse_Land_IgnoresBuildingColumnsWhenTheyArePresent()
    {
        using var file = Sheet(
            ["Plot Number", "Model Name", "Number of Floors", "Land Area", "Usable Area", "Selling Price"],
            ["P1", "H1", 2, 50m, 120m, 1_000_000m]);

        var unit = Assert.Single(ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid()));

        Assert.Null(unit.ModelType);
        Assert.Null(unit.NumberOfFloors);
        Assert.Null(unit.UsableArea);
    }

    [Theory]
    [InlineData("Plot Number")]
    [InlineData("Land Area")]
    [InlineData("Selling Price")]
    public void Parse_Land_RequiresPlotLandAreaAndPrice(string missing)
    {
        string[] all = ["Plot Number", "Land Area", "Selling Price"];
        using var file = Sheet(all.Where(h => h != missing).ToArray(), ["x", "y"]);

        var ex = Assert.Throws<BadRequestException>(
            () => ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid()));
        Assert.Contains(missing, ex.Message);
    }

    [Theory]
    [InlineData(null, "99/1", 50, 1_000_000, "Row 2: Plot Number is required.")]   // plot missing, house number present
    [InlineData("P1", null, null, 1_000_000, "Row 2")]   // land area blank
    [InlineData("P1", null, "abc", 1_000_000, "Row 2")]  // land area unparseable
    [InlineData("P1", null, 50, null, "Row 2: Selling Price must be a number greater than 0.")]          // price blank
    [InlineData("P1", null, 0, 1_000_000, "Row 2: Land Area must be a number greater than 0.")]      // land area zero
    [InlineData("P1", null, 50, 0, "Row 2: Selling Price must be a number greater than 0.")]             // price zero
    [InlineData("P1", null, -5, 1_000_000, "Row 2")]     // land area negative
    [InlineData("P1", null, 50, -1, "Row 2: Selling Price must be a number greater than 0.")]            // price negative
    public void Parse_Land_RejectsARowMissingPlotLandAreaOrPrice(
        string? plot, string? house, object? landArea, object? price, string expected)
    {
        using var file = Sheet(
            ["Plot Number", "House Number", "Land Area", "Selling Price"],
            [plot, house, landArea, price]);

        var ex = Assert.Throws<BadRequestException>(
            () => ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid()));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Parse_Land_NamesEveryFailingFieldOfTheRow()
    {
        using var file = Sheet(
            ["Plot Number", "Land Area", "Selling Price"],
            ["P1", 50m, 1m],
            ["P2", "x", 0m]);

        var ex = Assert.Throws<BadRequestException>(
            () => ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid()));
        Assert.Equal(
            "Row 3: Land Area must be a number greater than 0; Selling Price must be a number greater than 0.",
            ex.Message);
    }

    [Fact]
    public void Parse_Land_SkipsBlankRowsAndAKeylessTotalsRow()
    {
        using var file = Sheet(
            ["Plot Number", "House Number", "Land Area", "Selling Price"],
            ["P1", null, 50m, 1_000_000m],
            [null, null, null, null],
            ["P2", null, 60m, 1_200_000m],
            [null, null, 110m, 2_200_000m]); // totals row: no plot, no house

        var units = ProjectUnitExcelParser.Parse(file, ProjectType.Land, Guid.NewGuid());

        Assert.Equal(["P1", "P2"], units.Select(u => u.PlotNumber));
    }

    [Fact]
    public void Parse_LandAndBuilding_StillRequiresTheBuildingColumns()
    {
        using var file = Sheet(["Plot Number", "Land Area", "Selling Price"], ["P1", 50m, 1m]);

        Assert.Throws<BadRequestException>(
            () => ProjectUnitExcelParser.Parse(file, ProjectType.LandAndBuilding, Guid.NewGuid()));
    }

    [Fact]
    public void AttributesDiffer_Land_ComparesOnlyPriceAndLandArea()
    {
        var existing = PriceSheet.LbUnit(1, "P1", null, model: null, land: 50m, area: null, selling: 1_000_000m);
        // Differs only in fields a Land file never carries.
        var incoming = PriceSheet.LbUnit(1, "P1", null, model: "H9", land: 50m, area: 99m, selling: 1_000_000m);

        Assert.False(BlockReappraisalMatcher.AttributesDiffer(existing, incoming, ProjectType.Land, out var none));
        Assert.Empty(none);

        var changed = PriceSheet.LbUnit(1, "P1", null, model: null, land: 55m, area: null, selling: 1_100_000m);
        Assert.True(BlockReappraisalMatcher.AttributesDiffer(existing, changed, ProjectType.Land, out var diff));
        Assert.Equal(["sellingPrice", "landArea"], diff);
    }

    [Fact]
    public void AttributesDiffer_LandAndBuilding_StillComparesModelAndUsableArea()
    {
        var existing = PriceSheet.LbUnit(1, "P1", null, model: "H1", land: 50m, area: 100m, selling: 1m);
        var incoming = PriceSheet.LbUnit(1, "P1", null, model: "H2", land: 50m, area: 110m, selling: 1m);

        Assert.True(BlockReappraisalMatcher.AttributesDiffer(existing, incoming, ProjectType.LandAndBuilding, out var diff));
        Assert.Equal(["modelType", "usableArea"], diff);
    }
}
