using System.Net;
using System.Text.Json;
using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using ClosedXML.Excel;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Block "import unit prices from Excel" over HTTP against an isolated Testcontainers database:
/// preview writes nothing, import writes prices + source + the appraisal total (sold and model-less
/// units included), a blocking error refuses the whole file, calculate flips the source back and the
/// downloaded template uploads back without errors.
/// </summary>
[Collection("Integration")]
public class UnitPriceImportTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private sealed record Seeded(Guid AppraisalId, Guid Sold, Guid Normal, Guid NoModel, Guid Missing);

    /// <summary>
    /// Condo block with four units: "101" sold, "102" normal (model T1), "103" no model, "104" never in the
    /// file. "104" starts with a stored price so the import has something to clear.
    /// </summary>
    private async Task<Seeded> SeedCondoAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"UPI-{Guid.NewGuid():N}"[..18]);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(Ct);

        var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Import Test Condo");
        var sold = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/101", "101", "T1", 30m, 2_000_000m);
        var normal = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 2, 2, "A", "9/102", "102", "T1", 31m, 2_100_000m);
        var noModel = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 3, 3, "A", "9/103", "103", null, 32m, 2_200_000m);
        var missing = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 4, 4, "A", "9/104", "104", "T1", 33m, 2_300_000m);
        sold.MarkSoldByReappraisal();
        project.ImportUnits("units.xlsx", null, [sold, normal, noModel, missing]);
        // Calculate refuses a project without pricing assumptions.
        project.SetCondoPricingAssumption("AdjustPriceSqm", 0m, 0m, 0m, 0m, 0m, null, null, 80m);
        db.Projects.Add(project);

        var stale = ProjectUnitPrice.Create(missing.Id);
        stale.ApplyImportedValues(1m, 999m, 888m, 77m);
        db.ProjectUnitPrices.Add(stale);
        await db.SaveChangesAsync(Ct);

        return new Seeded(appraisal.Id, sold.Id, normal.Id, noModel.Id, missing.Id);
    }

    private async Task<(Guid AppraisalId, Guid Plot1, Guid Plot2)> SeedLbAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"UPL-{Guid.NewGuid():N}"[..18]);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(Ct);

        var project = Project.Create(appraisal.Id, ProjectType.LandAndBuilding, projectName: "Import Test LB");
        var u1 = ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, "12", "99/12", "H1", 2, 50m, 120m, 3_000_000m);
        var u2 = ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 2, "13", "99/13", "H1", 2, 55m, 125m, 3_100_000m);
        project.ImportUnits("units.xlsx", null, [u1, u2]);
        db.Projects.Add(project);
        await db.SaveChangesAsync(Ct);

        return (appraisal.Id, u1.Id, u2.Id);
    }

    // ── file + http helpers ─────────────────────────────────────────────────────────────────

    private static byte[] CondoFile(params object?[][] rows) => Workbook(UnitPriceImportLayout.Condo, rows);

    private static byte[] Workbook(UnitPriceImportLayout layout, object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        var headers = layout.AllHeaders;
        for (var c = 0; c < headers.Count; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // Condo columns: room, regNo, floor, tower, model, area, selling, std, appraisal, force, coverage.
    private static object?[] CondoRow(string? room, string? regNo, decimal? std, decimal? appraisal, decimal? force, decimal? coverage) =>
        [room, regNo, null, null, null, null, null, std, appraisal, force, coverage];

    // LB columns: plot, house, model, land, usable, selling, std, appraisal, force, coverage.
    private static object?[] LbRow(string plot, decimal std, decimal appraisal, decimal force, decimal coverage) =>
        [plot, null, null, null, null, null, std, appraisal, force, coverage];

    private Task<HttpResponseMessage> PostFileAsync(Guid appraisalId, string suffix, byte[] bytes, string fileName = "prices.xlsx")
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        return _client.PostAsync($"/appraisals/{appraisalId}/project/unit-prices/{suffix}", form, Ct);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    private async Task<JsonElement> GetSourceAsync(Guid appraisalId) =>
        await ReadJsonAsync(await _client.GetAsync($"/appraisals/{appraisalId}/project/unit-prices/source", Ct));

    private async Task<Dictionary<Guid, ProjectUnitPrice>> PricesAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.ProjectUnitPrices.AsNoTracking()
            .Where(p => db.ProjectUnits.Any(u => u.Id == p.ProjectUnitId
                        && db.Projects.Any(pr => pr.Id == u.ProjectId && pr.AppraisalId == appraisalId)))
            .ToDictionaryAsync(p => p.ProjectUnitId, Ct);
    }

    private async Task<(decimal Appraised, decimal? Forced, decimal? Insurance)?> ValuationAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var row = await db.ValuationAnalyses.AsNoTracking().FirstOrDefaultAsync(v => v.AppraisalId == appraisalId, Ct);
        return row is null ? null : (row.AppraisedValue, row.ForcedSaleValue, row.InsuranceValue);
    }

    /// <summary>101 sold, 102 normal, 103 no model (all priced), 104 omitted, plus a trailing totals row.</summary>
    private static byte[] StandardFile() => CondoFile(
        CondoRow("101", null, 40_000m, 1_000_000m, 800_000m, 100_000m),
        CondoRow("102", "9/102", 41_000m, 2_000_000.456m, 1_600_000m, 200_000m),
        CondoRow(null, "9/103", 42_000m, 3_000_000m, 2_400_000m, 300_000m),
        CondoRow(null, null, null, 6_000_000.46m, 4_800_000m, 600_000m));

    // ── preview ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_condo_reports_matches_and_writes_nothing()
    {
        var s = await SeedCondoAsync();

        var response = await PostFileAsync(s.AppraisalId, "import/preview", StandardFile());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("canImport").GetBoolean());
        Assert.Equal("Condo", body.GetProperty("projectType").GetString());
        Assert.Equal(3, body.GetProperty("matchedCount").GetInt32());
        Assert.Equal(4, body.GetProperty("unitsInProject").GetInt32());
        Assert.Equal(1, body.GetProperty("unitsMissingFromFile").GetArrayLength());
        Assert.True(body.GetProperty("sheetTotalsMatch").GetBoolean());

        // Nothing written: the stale price is still there, no new rows, source untouched, no total.
        var prices = await PricesAsync(s.AppraisalId);
        Assert.Equal([s.Missing], prices.Keys);
        Assert.Equal(999m, prices[s.Missing].TotalAppraisalValueRounded);
        Assert.Equal("Calculated", (await GetSourceAsync(s.AppraisalId)).GetProperty("source").GetString());
        Assert.Null(await ValuationAsync(s.AppraisalId));
    }

    [Fact]
    public async Task Preview_land_and_building_uses_the_lb_headers()
    {
        var (appraisalId, _, _) = await SeedLbAsync();
        var file = Workbook(UnitPriceImportLayout.LandAndBuilding,
            [LbRow("12", 25_000m, 1_500_000m, 1_200_000m, 100_000m), LbRow("13", 26_000m, 1_600_000m, 1_280_000m, 110_000m)]);

        var response = await PostFileAsync(appraisalId, "import/preview", file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("LandAndBuilding", body.GetProperty("projectType").GetString());
        Assert.True(body.GetProperty("canImport").GetBoolean());
        Assert.Equal(2, body.GetProperty("matchedCount").GetInt32());
        Assert.Equal(0, body.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task Preview_of_a_condo_file_against_an_lb_project_is_blocked_on_headers()
    {
        var (appraisalId, _, _) = await SeedLbAsync();

        var response = await PostFileAsync(appraisalId, "import/preview", CondoFile(CondoRow("12", null, null, 1m, null, null)));

        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("canImport").GetBoolean());
        Assert.Equal("MissingHeader", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Upload_that_is_not_xlsx_is_a_400()
    {
        var s = await SeedCondoAsync();

        var response = await PostFileAsync(s.AppraisalId, "import/preview", [1, 2, 3], "prices.csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preview_for_an_unknown_appraisal_is_404()
    {
        var response = await PostFileAsync(Guid.NewGuid(), "import/preview", CondoFile(CondoRow("101", null, null, 1m, null, null)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── import ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_writes_prices_source_and_a_plain_sum_over_every_unit()
    {
        var s = await SeedCondoAsync();

        var response = await PostFileAsync(s.AppraisalId, "import", StandardFile(), "july-prices.xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var prices = await PricesAsync(s.AppraisalId);
        // Sold (101) and model-less (103) units are priced like any other.
        Assert.Equal(1_000_000m, prices[s.Sold].TotalAppraisalValueRounded);
        Assert.Equal(40_000m, prices[s.Sold].StandardPrice);
        Assert.Equal(800_000m, prices[s.Sold].ForceSellingPrice);
        Assert.Equal(100_000m, prices[s.Sold].CoverageAmount);
        // 2 dp rounding, file value as is (no 1,000 rounding).
        Assert.Equal(2_000_000.46m, prices[s.Normal].TotalAppraisalValueRounded);
        Assert.Equal(3_000_000m, prices[s.NoModel].TotalAppraisalValueRounded);
        // 104 is not in the file: its old figure is cleared, never kept beside the imported ones.
        Assert.Null(prices[s.Missing].TotalAppraisalValueRounded);
        Assert.Null(prices[s.Missing].ForceSellingPrice);
        Assert.Null(prices[s.Missing].CoverageAmount);

        var source = await GetSourceAsync(s.AppraisalId);
        Assert.Equal("Imported", source.GetProperty("source").GetString());
        Assert.Equal("july-prices.xlsx", source.GetProperty("fileName").GetString());
        Assert.NotNull(source.GetProperty("importedAt").GetString());
        // 101, 102 and 103 are priced; 104 was cleared.
        Assert.Equal(3, source.GetProperty("pricedUnitCount").GetInt32());

        var valuation = await ValuationAsync(s.AppraisalId);
        Assert.NotNull(valuation);
        Assert.Equal(1_000_000m + 2_000_000.46m + 3_000_000m, valuation!.Value.Appraised);
        Assert.Equal(800_000m + 1_600_000m + 2_400_000m, valuation.Value.Forced);
        Assert.Equal(100_000m + 200_000m + 300_000m, valuation.Value.Insurance);
    }

    [Fact]
    public async Task Import_land_and_building_sets_source_and_total()
    {
        var (appraisalId, plot1, plot2) = await SeedLbAsync();
        var file = Workbook(UnitPriceImportLayout.LandAndBuilding,
            [LbRow("12", 25_000m, 1_500_000m, 1_200_000m, 100_000m), LbRow("13", 26_000m, 1_600_000m, 1_280_000m, 110_000m)]);

        var response = await PostFileAsync(appraisalId, "import", file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prices = await PricesAsync(appraisalId);
        Assert.Equal(1_500_000m, prices[plot1].TotalAppraisalValueRounded);
        Assert.Equal(1_280_000m, prices[plot2].ForceSellingPrice);
        Assert.Equal("Imported", (await GetSourceAsync(appraisalId)).GetProperty("source").GetString());
        var valuation = await ValuationAsync(appraisalId);
        Assert.Equal(3_100_000m, valuation!.Value.Appraised);
        Assert.Equal(2_480_000m, valuation.Value.Forced);
        Assert.Equal(210_000m, valuation.Value.Insurance);
    }

    [Fact]
    public async Task Import_with_a_blocking_error_is_a_400_and_writes_nothing()
    {
        var s = await SeedCondoAsync();
        var file = CondoFile(
            CondoRow("101", null, 40_000m, 1_000_000m, 800_000m, 100_000m),
            CondoRow("777", null, 41_000m, 2_000_000m, 1_600_000m, 200_000m)); // no such unit

        var response = await PostFileAsync(s.AppraisalId, "import", file);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var prices = await PricesAsync(s.AppraisalId);
        Assert.Equal([s.Missing], prices.Keys);
        Assert.Equal(999m, prices[s.Missing].TotalAppraisalValueRounded);
        Assert.Equal("Calculated", (await GetSourceAsync(s.AppraisalId)).GetProperty("source").GetString());
        Assert.Null(await ValuationAsync(s.AppraisalId));
    }

    [Fact]
    public async Task Import_of_a_file_that_matches_no_unit_is_a_400()
    {
        var s = await SeedCondoAsync();

        var response = await PostFileAsync(s.AppraisalId, "import", CondoFile());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Calculated", (await GetSourceAsync(s.AppraisalId)).GetProperty("source").GetString());
    }

    // ── calculate flips the source back ─────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_after_an_import_puts_the_source_back_to_Calculated_and_keeps_the_history()
    {
        var s = await SeedCondoAsync();
        Assert.Equal(HttpStatusCode.OK, (await PostFileAsync(s.AppraisalId, "import", StandardFile(), "kept.xlsx")).StatusCode);

        var calc = await _client.PostAsync($"/appraisals/{s.AppraisalId}/project/unit-prices/calculate", null, Ct);

        Assert.True(calc.StatusCode == HttpStatusCode.NoContent, await calc.Content.ReadAsStringAsync(Ct));
        var source = await GetSourceAsync(s.AppraisalId);
        Assert.Equal("Calculated", source.GetProperty("source").GetString());
        Assert.Equal("kept.xlsx", source.GetProperty("fileName").GetString());
    }

    // ── template round trip ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Downloaded_template_uploads_back_with_no_errors_and_no_differences()
    {
        var s = await SeedCondoAsync();
        Assert.Equal(HttpStatusCode.OK, (await PostFileAsync(s.AppraisalId, "import", StandardFile())).StatusCode);

        var download = await _client.GetAsync($"/appraisals/{s.AppraisalId}/project/unit-prices/template", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var template = await download.Content.ReadAsByteArrayAsync(Ct);

        var preview = await PostFileAsync(s.AppraisalId, "import/preview", template);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await ReadJsonAsync(preview);
        // 104 has no price after the import: its row is all blank, which means "no price" (listed as
        // missing from the file, cleared on import), not an error.
        Assert.Equal(0, body.GetProperty("errors").GetArrayLength());
        Assert.Equal(0, body.GetProperty("attributeDiffs").GetArrayLength());
        // One row per unit, sold included; the unpriced one is not matched for import.
        Assert.Equal(4, body.GetProperty("rowCount").GetInt32());
        Assert.Equal(3, body.GetProperty("matchedCount").GetInt32());
        Assert.Equal(1, body.GetProperty("unitsMissingFromFile").GetArrayLength());
        Assert.True(body.GetProperty("canImport").GetBoolean());
    }

    [Fact]
    public async Task Downloaded_template_of_a_fully_priced_project_has_zero_errors()
    {
        var s = await SeedCondoAsync();
        // Price every unit including the one the standard file leaves out.
        var all = CondoFile(
            CondoRow("101", null, 40_000m, 1_000_000m, 800_000m, 100_000m),
            CondoRow("102", null, 41_000m, 2_000_000m, 1_600_000m, 200_000m),
            CondoRow("103", null, 42_000m, 3_000_000m, 2_400_000m, 300_000m),
            CondoRow("104", null, 43_000m, 4_000_000m, 3_200_000m, 400_000m));
        Assert.Equal(HttpStatusCode.OK, (await PostFileAsync(s.AppraisalId, "import", all)).StatusCode);

        var template = await (await _client.GetAsync($"/appraisals/{s.AppraisalId}/project/unit-prices/template", Ct))
            .Content.ReadAsByteArrayAsync(Ct);
        var body = await ReadJsonAsync(await PostFileAsync(s.AppraisalId, "import/preview", template));

        Assert.Equal(0, body.GetProperty("errors").GetArrayLength());
        Assert.Equal(0, body.GetProperty("attributeDiffs").GetArrayLength());
        Assert.True(body.GetProperty("canImport").GetBoolean());
        Assert.Equal(4, body.GetProperty("matchedCount").GetInt32());
    }
}
