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
/// What is under test is that progress and value come from the stored Summary* figures — which the
/// aggregate writes in BOTH modes (summary: keyed percent, money derived from it; full detail: summed from
/// the work rows) — through appraisal.vw_AppraisalConstructionProgress, with no branching on the mode here.
/// A figure the client sends for the two money columns is ignored by the aggregate, so a payload carrying a
/// stale 0 (what the CI screen sent) cannot drop the part-built building from the regulatory file.
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
            summaryCurrentProgressPct: 50m,
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
            summaryCurrentProgressPct: 25m,
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
            summaryCurrentProgressPct: 0m,
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
            summaryCurrentProgressPct: null,
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
            summaryCurrentProgressPct: 100m,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(7_500_000m, result.InspectedCurrentValue);
        Assert.Equal(result.InspectedTotalValue, result.InspectedCurrentValue);
        // Previous round: 7,500,000 × 50%.
        Assert.Equal(3_750_000m, result.InspectedPreviousValue);
    }

    // ── Full-detail mode reads the same stored figures ────────────────────────

    [Fact]
    public async Task FullDetailMode_ReportsTheSummedWorkRowsThroughTheStoredFigures()
    {
        var group = Guid.NewGuid();
        var inspection = ConstructionInspection.CreateFullDetail(Guid.Empty, 8_000_000m, remark: null);
        inspection.AddWorkDetail(group, "structure", 1, 50m, previousProgressPct: 20m, currentProgressPct: 100m);
        inspection.AddWorkDetail(group, "finishing", 2, 50m, previousProgressPct: 0m, currentProgressPct: 50m);
        inspection.ComputeAllValues();

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.Equal(6_000_000m, result.InspectedCurrentValue);   // 4M x 100% + 4M x 50%
        Assert.Equal(800_000m, result.InspectedPreviousValue);    // 4M x 20%
        Assert.Equal(75m, result.ConstructionProgressPercent);
        Assert.Equal(10m, result.PreviousProgressPercent);
        Assert.True(result.IsUnderConstruction);
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
    public async Task ZeroTotalValue_StillReportsTheRecordedProgress()
    {
        // An inspection with nothing to value used to be treated as "no inspection". It is not:
        // half a building is being built whether or not anyone has priced it yet, and the
        // percentage is recorded. Returning null here sent IsUnderConstruction and
        // ConstructionProgressPercent as null onto the frozen collateral engagement — recording a
        // part-built collateral as not under construction — and blanked the MIS progress columns.
        // The money comes out 0, which is what "not priced yet" looks like and cannot be read as
        // "finished" because the percentage says otherwise.
        var inspection = ConstructionInspection.CreateSummary(
            Guid.Empty, totalValue: 0m,
            summaryDetail: null,
            summaryPreviousProgressPct: null,
            summaryCurrentProgressPct: 50m,
            remark: null);

        var result = await SeedAndGetAsync(inspection);

        Assert.NotNull(result);
        Assert.False(result.HasOwnValueBase);
        Assert.Equal(50m, result.ConstructionProgressPercent);
        Assert.True(result.IsUnderConstruction);
        Assert.Equal(0m, result.CompleteValue);
        Assert.Equal(0m, result.CurrentValue);
    }
}
