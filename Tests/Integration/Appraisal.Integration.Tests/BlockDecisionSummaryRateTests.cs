using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// A block's forced-sale total is the sum of the units' own ForceSellingPrice, whatever the rate, and the rate it
/// shows is that sum over the total appraisal (so it always matches the amounts); with no total it falls back
/// to the project's effective %.
/// </summary>
[Collection("Integration")]
public class BlockDecisionSummaryRateTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> SeedBlockAsync(
        int? ratePercent, bool withAssumption = true, decimal appraisalValue = 1_000_000m, decimal forcedSale = 123_000m)
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"DSR-{Guid.NewGuid():N}"[..18]);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(Ct);

        var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Rate Test");
        var unit = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", "T1", 30m, 1m);
        project.ImportUnits("units.xlsx", null, [unit]);
        if (withAssumption)
            project.SetCondoPricingAssumption("AdjustPriceSqm", 0m, 0m, 0m, 0m, 0m, null, null, ratePercent);
        db.Projects.Add(project);

        // The unit's forced-sale price is deliberately NOT total x rate: the summary must report it as stored.
        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(1m, appraisalValue, forcedSale, 10_000m);
        db.ProjectUnitPrices.Add(price);
        await db.SaveChangesAsync(Ct);
        return appraisal.Id;
    }

    private async Task<JsonElement> GetSummaryAsync(Guid appraisalId)
    {
        var response = await _client.GetAsync($"/appraisals/{appraisalId}/decision-summary", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
    }

    [Theory]
    [InlineData(85)]
    [InlineData(0)]
    [InlineData(null)]
    public async Task Rate_shown_is_the_unit_sum_over_the_total_whatever_the_assumption_says(int? rate)
    {
        var appraisalId = await SeedBlockAsync(rate);

        var summary = await GetSummaryAsync(appraisalId);

        Assert.True(summary.GetProperty("isBlock").GetBoolean());
        Assert.Equal(123_000m, summary.GetProperty("forceSellingPrice").GetDecimal());
        Assert.Equal(12.30m, summary.GetProperty("forceSellingRate").GetDecimal());   // 123,000 / 1,000,000
    }

    [Theory]
    [InlineData(85)]
    [InlineData(0)]   // 0% is a real rate, not "blank"
    public async Task With_no_total_the_rate_falls_back_to_the_projects_own(int rate)
    {
        var appraisalId = await SeedBlockAsync(rate, appraisalValue: 0m, forcedSale: 0m);

        Assert.Equal(rate, (await GetSummaryAsync(appraisalId)).GetProperty("forceSellingRate").GetDecimal());
    }

    [Fact]
    public async Task With_no_total_and_a_blank_assumption_the_rate_is_the_system_default()
    {
        decimal expected;
        await using (var conn = new SqlConnection(Fixture.ConnectionString))
        {
            var configured = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT TOP (1) [Value] FROM common.SystemConfigurations WHERE [Key] = N'ForceSaleRateDefaultPct' AND IsActive = 1",
                cancellationToken: Ct));
            expected = decimal.TryParse(configured, out var d) && d is >= 0 and <= 100 ? d : 70m;
        }
        var blank = await SeedBlockAsync(null, appraisalValue: 0m, forcedSale: 0m);
        var none = await SeedBlockAsync(null, withAssumption: false, appraisalValue: 0m, forcedSale: 0m);

        Assert.Equal(expected, (await GetSummaryAsync(blank)).GetProperty("forceSellingRate").GetDecimal());
        Assert.Equal(expected, (await GetSummaryAsync(none)).GetProperty("forceSellingRate").GetDecimal());
    }

    [Theory]
    [InlineData(10_000_000, 10_000_000, 7_000_000)]   // typed = unit total -> exactly the unit sum
    [InlineData(9_000_000, 10_000_000, 6_300_000)]    // typed 9M on a 10M / 7M block -> 6.3M
    public async Task Book_verification_scales_the_unit_forced_sale_by_the_typed_total(
        int typed, int unitTotal, int expectedForcedSale)
    {
        var appraisalId = await SeedBlockAsync(70, appraisalValue: unitTotal, forcedSale: 7_000_000m);

        var save = await _client.PostAsJsonAsync(
            $"/appraisals/{appraisalId}/decision-summary",
            new { isPriceVerified = true, totalAppraisalPriceReview = (decimal)typed },
            Ct);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var summary = await GetSummaryAsync(appraisalId);
        Assert.Equal(typed, summary.GetProperty("totalAppraisalPriceReview").GetDecimal());
        Assert.Equal(expectedForcedSale, summary.GetProperty("forceSellingPriceReview").GetDecimal());
    }
}
