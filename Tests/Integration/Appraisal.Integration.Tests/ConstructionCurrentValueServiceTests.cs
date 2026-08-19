using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Pins <see cref="IConstructionCurrentValueService"/> — the single source of the part-built
/// ("current") value, shared by the Decision Summary construction card and the regulatory export's
/// Appraisal-Value-as-Completed field.
///
/// The rule under test that is easy to get wrong: <b>summary mode derives the value from the stored
/// percent, NOT from the stored value.</b> The CI screen computes the figure in a useMemo and displays
/// it but never writes it back into the form, so <c>SummaryCurrentValue</c> in the database holds the
/// default 0 while the screen showed something else. Reading that column would silently drop the whole
/// part-built building from the regulatory file.
///
/// Land and completed-building components are deliberately left unseeded (no pricing rows, no
/// depreciation rows) so they contribute 0 and each assertion isolates the inspection arithmetic.
/// </summary>
[Collection("Integration")]
public class ConstructionCurrentValueServiceTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static AppraisalAggregate CreateAppraisalSeed()
    {
        var a = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..18]);
        // Status flow is bypassed on purpose — these tests exercise the read path only.
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, DateTime.Now);
        return a;
    }

    /// <summary>Adds a land property carrying the given inspection, wiring the owned entity's FK.</summary>
    private static void AttachLandPropertyWithInspection(
        AppraisalAggregate appraisal, ConstructionInspection inspection)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        prop.SetConstructionInspection(inspection);

        // ConstructionInspection is an owned entity keyed by AppraisalPropertyId; the property id is
        // only known after AddLandProperty, so the FK is set here (same approach as
        // CollateralUpsertServiceTests).
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(inspection, prop.Id);
    }

    private async Task<ConstructionValueBreakdown?> SeedAndGetAsync(ConstructionInspection inspection)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = CreateAppraisalSeed();
        AttachLandPropertyWithInspection(appraisal, inspection);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<IConstructionCurrentValueService>();
        return await service.GetAsync(appraisal.Id, TestContext.Current.CancellationToken);
    }

    // ── Summary mode: percent wins, stored value is ignored ───────────────────

    [Fact]
    public async Task SummaryMode_DerivesCurrentValueFromPercent_NotFromStoredValue()
    {
        // The frontend bug reproduced: percent is correct (50%), stored value is the default 0.
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 10_000_000m,
            summaryDetail: "Overall progress",
            summaryPreviousProgressPct: 0m,
            summaryPreviousValue: 0m,
            summaryCurrentProgressPct: 50m,
            summaryCurrentValue: 0m,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        // 10,000,000 × 50% — NOT the stored 0.
        Assert.Equal(5_000_000m, result.InspectedCurrentValue);
        Assert.Equal(10_000_000m, result.InspectedTotalValue);
        // Nothing else seeded, so the appraisal-level current value is the inspection value alone.
        Assert.Equal(5_000_000m, result.CurrentValue);
    }

    [Fact]
    public async Task SummaryMode_IgnoresStoredValue_EvenWhenItDisagreesWithThePercent()
    {
        // A stale/incorrect stored value must not leak into the figure.
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 10_000_000m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: 25m,
            summaryCurrentValue: 9_999_999m,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(2_500_000m, result.InspectedCurrentValue);
    }

    [Fact]
    public async Task SummaryMode_ZeroPercent_YieldsZeroForTheBuilding()
    {
        // Not started: the building contributes nothing. Land would still count, but none is seeded.
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 10_000_000m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: 0m,
            summaryCurrentValue: null,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(0m, result.InspectedCurrentValue);
        Assert.Equal(10_000_000m, result.InspectedTotalValue);
    }

    [Fact]
    public async Task SummaryMode_NullPercent_IsTreatedAsZero()
    {
        // A freshly carried-forward round has no percent yet (CopyForNextInspection nulls it).
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 8_000_000m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: null,
            summaryCurrentValue: null,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(0m, result.InspectedCurrentValue);
    }

    [Fact]
    public async Task SummaryMode_HundredPercent_CurrentEqualsTotal()
    {
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 7_500_000m,
            summaryDetail: null,
            summaryPreviousProgressPct: 50m,
            summaryPreviousValue: 0m,
            summaryCurrentProgressPct: 100m,
            summaryCurrentValue: 0m,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(7_500_000m, result.InspectedCurrentValue);
        Assert.Equal(result.InspectedTotalValue, result.InspectedCurrentValue);
        // Previous round: 7,500,000 × 50%.
        Assert.Equal(3_750_000m, result.InspectedPreviousValue);
    }

    // ── No inspection at all ──────────────────────────────────────────────────

    [Fact]
    public async Task NoInspection_ReturnsNull()
    {
        // Nothing part-built → no "current" value distinct from the appraised value, so the contract
        // sends NULL and the regulatory writer falls back to the appraised value.
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = CreateAppraisalSeed();
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<IConstructionCurrentValueService>();
        var result = await service.GetAsync(appraisal.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ZeroTotalValue_ReturnsNull()
    {
        // An inspection with nothing to value is treated as "no inspection" — mirrors the previous
        // Decision Summary behaviour, which hid the card when SUM(TotalValue) was 0.
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 0m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryPreviousValue: null,
            summaryCurrentProgressPct: 50m,
            summaryCurrentValue: null,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.Null(result);
    }
}
