using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Runs Database/Migration/Scripts/20261016120000_DataFix_BackfillBlockUnitForceSellingPrice.sql against the
/// isolated test database and checks the rate chain: project assumption (0% included), else the active
/// system default, else 70; rows that already have a forced-sale price or no appraisal value stay untouched.
/// </summary>
[Collection("Integration")]
public class BackfillBlockUnitForceSellingPriceScriptTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string ScriptName = "20261016120000_DataFix_BackfillBlockUnitForceSellingPrice.sql";

    private static string[] ScriptBatches()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Database", "Migration", "Scripts", ScriptName)))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var sql = File.ReadAllText(Path.Combine(dir!.FullName, "Database", "Migration", "Scripts", ScriptName));
        return Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToArray();
    }

    private async Task RunScriptAsync()
    {
        await using var conn = new SqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(Ct);
        foreach (var batch in ScriptBatches())
            await conn.ExecuteAsync(new CommandDefinition(batch, cancellationToken: Ct));
    }

    /// <summary>A one-unit Condo block whose unit has the given appraisal value and no forced-sale price.</summary>
    private async Task<Guid> SeedUnitAsync(decimal? appraisalValue, decimal? forcedSale, int? ratePercent, bool withAssumption = true)
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"BKF-{Guid.NewGuid():N}"[..18]);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(Ct);

        var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Backfill Test");
        var unit = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", null, 30m, 1m);
        project.ImportUnits("units.xlsx", null, [unit]);
        if (withAssumption)
            project.SetCondoPricingAssumption("AdjustPriceSqm", 0m, 0m, 0m, 0m, 0m, null, null, ratePercent);
        db.Projects.Add(project);

        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(1m, appraisalValue, forcedSale, 10m);
        db.ProjectUnitPrices.Add(price);
        await db.SaveChangesAsync(Ct);
        return unit.Id;
    }

    private async Task<decimal?> ForcedSaleOfAsync(Guid unitId)
    {
        await using var conn = new SqlConnection(fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT ForceSellingPrice FROM appraisal.ProjectUnitPrices WHERE ProjectUnitId = @unitId",
            new { unitId }, cancellationToken: Ct));
    }

    [Fact]
    public async Task Backfill_fills_only_missing_forced_sale_prices_by_the_rate_chain_and_is_idempotent()
    {
        await using (var conn = new SqlConnection(fixture.ConnectionString))
        {
            var active = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT TOP (1) [Value] FROM common.SystemConfigurations WHERE [Key] = N'ForceSaleRateDefaultPct' AND IsActive = 1",
                cancellationToken: Ct));
            // The fallback case below is only deterministic against a known default (70 when unset).
            Assert.True(active is null || decimal.TryParse(active, out _));
            DefaultPct = active is null ? 70m : decimal.Parse(active);
        }

        var withRate = await SeedUnitAsync(1_234_567m, null, 80);
        var zeroRate = await SeedUnitAsync(1_000_000m, null, 0);
        var blankRate = await SeedUnitAsync(1_000_000m, null, null);
        var noAssumption = await SeedUnitAsync(1_000_000m, null, null, withAssumption: false);
        var alreadyPriced = await SeedUnitAsync(1_000_000m, 123_000m, 80);
        var noValue = await SeedUnitAsync(null, null, 80);

        await RunScriptAsync();

        Assert.Equal(988_000m, await ForcedSaleOfAsync(withRate));            // 1,234,567 x 80% -> nearest 1,000
        Assert.Equal(0m, await ForcedSaleOfAsync(zeroRate));                  // 0% is a rate, not "blank"
        var fromDefault = Math.Round(1_000_000m * DefaultPct / 100m / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;
        Assert.Equal(fromDefault, await ForcedSaleOfAsync(blankRate));
        Assert.Equal(fromDefault, await ForcedSaleOfAsync(noAssumption));
        Assert.Equal(123_000m, await ForcedSaleOfAsync(alreadyPriced));       // never overwritten
        Assert.Null(await ForcedSaleOfAsync(noValue));                        // nothing to base it on

        await RunScriptAsync();

        Assert.Equal(988_000m, await ForcedSaleOfAsync(withRate));
        Assert.Equal(0m, await ForcedSaleOfAsync(zeroRate));
        Assert.Equal(123_000m, await ForcedSaleOfAsync(alreadyPriced));
    }

    private decimal DefaultPct;
}
