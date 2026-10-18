using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

public class UnitPriceTemplateRoundTripTests
{
    private static ProjectUnitPrice Priced(ProjectUnit unit, decimal std, decimal appraisal, decimal force, decimal coverage)
    {
        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(std, appraisal, force, coverage);
        return price;
    }

    [Fact]
    public void Condo_DownloadedTemplateUploadsBackWithoutErrorsOrDifferences()
    {
        var units = new[]
        {
            PriceSheet.CondoUnit(1, "101", "9/101", 3, "A", "T1", 35.5m, 2_000_000m),
            PriceSheet.CondoUnit(2, "102", null, 4, "A", "T2", 40.25m, 2_500_000m),
            PriceSheet.CondoUnit(3, null, "9/103", null, null, null, null, null)
        };
        var prices = new[]
        {
            Priced(units[0], 40_000m, 1_408_945_000.5m, 986_000m, 397_295m),
            Priced(units[1], 41_000m, 1_000_000m, 700_000m, 10_000m),
            Priced(units[2], 1m, 2m, 3m, 4m)
        }.ToDictionary(p => p.ProjectUnitId);

        var bytes = PriceSheet.Template(UnitPriceImportLayout.Condo, units, prices);
        using var file = new MemoryStream(bytes);
        var sheet = UnitPriceExcelParser.Parse(file, UnitPriceImportLayout.Condo);
        var result = PriceSheet.AnalyzeWith(
            "t.xlsx", sheet, UnitPriceImportLayout.Condo, units,
            prices.ToDictionary(kv => kv.Key, kv => kv.Value.TotalAppraisalValueRounded));

        var r = result.Response;
        Assert.Empty(r.Errors);
        Assert.Empty(r.AttributeDiffs);
        Assert.Empty(r.UnitsMissingFromFile);
        Assert.Null(r.SheetTotals);
        Assert.Equal(3, r.MatchedCount);
        Assert.True(r.CanImport);
        Assert.All(r.Rows, row => Assert.Equal(row.PreviousAppraisalValue, row.NewAppraisalValue));
        Assert.Equal(1_408_945_000.5m + 1_000_000m + 2m, r.Totals.AppraisalValue);
    }

    [Fact]
    public void LandAndBuilding_DownloadedTemplateUploadsBackWithoutErrorsOrDifferences()
    {
        var units = new[]
        {
            PriceSheet.LbUnit(1, "12", "99/12", "Model-A", 50.25m, 120m, 3_000_000m),
            PriceSheet.LbUnit(2, "13", "99/13", "Model-B", 60m, 130m, 3_500_000m)
        };
        var prices = units.Select(u => Priced(u, 25_000m, 2_900_000m, 1_800_000m, 400_000m))
            .ToDictionary(p => p.ProjectUnitId);

        var bytes = PriceSheet.Template(UnitPriceImportLayout.LandAndBuilding, units, prices);
        using var file = new MemoryStream(bytes);
        var sheet = UnitPriceExcelParser.Parse(file, UnitPriceImportLayout.LandAndBuilding);
        var result = PriceSheet.AnalyzeWith(
            "t.xlsx", sheet, UnitPriceImportLayout.LandAndBuilding, units, new Dictionary<Guid, decimal?>());

        Assert.Empty(result.Response.Errors);
        Assert.Empty(result.Response.AttributeDiffs);
        Assert.Equal(2, result.Response.MatchedCount);
    }

    [Fact]
    public void SameRoomNumberInTwoTowers_WithoutRegistrationNumbers_RoundTripsThroughTheTemplate()
    {
        var a = PriceSheet.CondoUnit(1, "101", null, 1, "Tower A");
        var b = PriceSheet.CondoUnit(2, "101", null, 1, "tower b");
        var prices = new[] { Priced(a, 1m, 1_000m, 700m, 10m), Priced(b, 1m, 2_000m, 1_400m, 20m) }
            .ToDictionary(p => p.ProjectUnitId);

        using var file = new MemoryStream(PriceSheet.Template(UnitPriceImportLayout.Condo, [a, b], prices));
        var result = PriceSheet.AnalyzeWith(
            "t.xlsx", UnitPriceExcelParser.Parse(file, UnitPriceImportLayout.Condo), UnitPriceImportLayout.Condo,
            [a, b], new Dictionary<Guid, decimal?>());

        Assert.Empty(result.Response.Errors);
        Assert.Equal(2, result.Response.MatchedCount);
        Assert.Equal(1_000m, result.Matched[a.Id].AppraisalValue);
        Assert.Equal(2_000m, result.Matched[b.Id].AppraisalValue);
    }

    [Fact]
    public void PricedUnitWithoutInsurance_RoundTripsWithoutErrors()
    {
        var unit = PriceSheet.CondoUnit(1, "101");
        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(1m, 1_000m, 700m, null);   // insurance never stored
        var prices = new Dictionary<Guid, ProjectUnitPrice> { [unit.Id] = price };

        using var file = new MemoryStream(PriceSheet.Template(UnitPriceImportLayout.Condo, [unit], prices));
        var result = PriceSheet.AnalyzeWith(
            "t.xlsx", UnitPriceExcelParser.Parse(file, UnitPriceImportLayout.Condo), UnitPriceImportLayout.Condo,
            [unit], new Dictionary<Guid, decimal?>());

        Assert.Empty(result.Response.Errors);
        Assert.True(result.Response.CanImport);
        Assert.Null(result.Matched[unit.Id].CoverageAmount);
    }

    [Fact]
    public void UnitsWithNeitherKey_AreLeftOutOfTheTemplate()
    {
        var keyed = PriceSheet.CondoUnit(1, "101");
        var keyless = PriceSheet.CondoUnit(2, null, "  ", floor: 4);
        var price = ProjectUnitPrice.Create(keyless.Id);
        price.ApplyImportedValues(1m, 2m, 3m, 4m);

        var bytes = PriceSheet.Template(
            UnitPriceImportLayout.Condo, [keyed, keyless], new Dictionary<Guid, ProjectUnitPrice> { [keyless.Id] = price });
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheets.First();

        Assert.Equal(2, ws.LastRowUsed()!.RowNumber());
        Assert.Equal("101", ws.Cell(2, 1).GetString());
    }

    [Fact]
    public void Template_HasHeadersInOrder_AndOneRowPerUnit_PricesBlankWhenNotStored()
    {
        var units = new[] { PriceSheet.CondoUnit(1, "101"), PriceSheet.CondoUnit(2, "102") };

        var bytes = PriceSheet.Template(
            UnitPriceImportLayout.Condo, units, new Dictionary<Guid, ProjectUnitPrice>());
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheets.First();

        var expected = UnitPriceImportLayout.Condo.AllHeaders;
        for (var c = 0; c < expected.Count; c++)
            Assert.Equal(expected[c], ws.Cell(1, c + 1).GetString());
        Assert.Equal(3, ws.LastRowUsed()!.RowNumber());
        Assert.Equal("101", ws.Cell(2, 1).GetString());
        Assert.True(ws.Cell(2, expected.Count).IsEmpty());
    }
}
