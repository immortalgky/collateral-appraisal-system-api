using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// PR-8 integration tests covering the wiring of UnitPrice / BuildingCost / AppraisalValue
/// from PricingFinalValue on the selected cost-approach method.
///
/// Field mapping (per user spec — FinalValueAdjust → actual schema field FinalValueAdjusted):
///   UnitPrice     ← PricingFinalValue.FinalValueAdjusted    (cost approach only)
///   BuildingCost  ← PricingFinalValue.BuildingValue           (cost approach only)
///   AppraisalValue← PricingFinalValue.AppraisalPrice         (all approaches)
///                   ?? PricingFinalValue.FinalValueAdjusted
///                   ?? PricingFinalValue.FinalValueRounded
/// </summary>
[Collection("Integration")]
public class CollateralPR8_PricingFinalValueTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId)
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"PR8-{Guid.NewGuid():N}"[..15]);
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create(subDistrict, district, province), landOffice: landOffice);
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        return prop;
    }

    /// <summary>
    /// Seeds a PricingAnalysis with one cost-approach method that has BuildingCost, AppraisalPrice,
    /// and FinalValueAdjusted set distinctly. Returns the created analysis.
    ///
    /// Field mapping (post user-correction):
    ///   UnitPrice     ← FinalValue.FinalValueAdjusted   (the "adjusted unit price" rate)
    ///   BuildingCost  ← FinalValue.BuildingValue
    ///   AppraisalValue← FinalValue.AppraisalPrice ?? FinalValueAdjusted ?? FinalValueRounded
    /// </summary>
    private static PricingAnalysis SeedCostApproachPricing(
        Guid propertyGroupId,
        decimal finalValueAdjusted,
        decimal buildingCost,
        decimal appraisalPrice)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(propertyGroupId);
        pa.StartProgress();

        var approach = pa.AddApproach("Cost");
        approach.Select();

        var method = approach.AddMethod("WQS"); // method type under Cost approach
        method.SetAsSelected();
        method.SetValue(appraisalPrice);

        // PricingFinalValue.Create(methodId, finalValueAdjusted, finalValueRounded)
        var fv = PricingFinalValue.Create(method.Id, finalValueAdjusted, appraisalPrice);
        fv.SetBuildingValue(buildingCost);
        fv.SetAppraisalPrice(appraisalPrice);
        method.SetFinalValue(fv);

        pa.SetFinalValues(appraisalPrice);
        return pa;
    }

    /// <summary>
    /// Seeds a PricingAnalysis with one non-cost-approach method (Market) that has
    /// AppraisalPrice set but no BuildingCost.
    /// </summary>
    private static PricingAnalysis SeedMarketApproachPricing(
        Guid propertyGroupId,
        decimal appraisalPrice)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(propertyGroupId);
        pa.StartProgress();

        var approach = pa.AddApproach("Market");
        approach.Select();

        var method = approach.AddMethod("WQS");
        method.SetAsSelected();
        method.SetValue(appraisalPrice);

        var fv = PricingFinalValue.Create(method.Id, appraisalPrice, appraisalPrice);
        fv.SetAppraisalPrice(appraisalPrice);
        method.SetFinalValue(fv);

        pa.SetFinalValues(appraisalPrice);
        return pa;
    }

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private CollateralDbContext GetCollateralDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    private async Task ProcessAppraisalInNewScopeAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);
    }

    // -----------------------------------------------------------------------
    // PR8-1: Cost approach, single property — all three values populated on IsMaster
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR8_1_CostApproach_SingleProperty_AllThreeValuesPopulated()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var titleNo = $"PR8-1-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);

            var a = CreateAppraisalSeed(Guid.NewGuid());
            var prop = SeedLandProperty(a, "LO-PR8", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;

            // Create a PropertyGroup, add the property to it
            var group = a.CreateGroup("Test Group");
            group.AddProperty(prop.Id);

            // Seed cost-approach pricing for this group — distinct values for each field
            var pa = SeedCostApproachPricing(
                propertyGroupId: group.Id,
                finalValueAdjusted: 12_000m,   // adjusted unit price (per sq.wa)
                buildingCost: 500_000m,        // 500k building cost
                appraisalPrice: 1_500_000m);   // 1.5M final total

            appraisalDb.PricingAnalyses.Add(pa);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        Assert.True(master.IsMaster);

        var ld = master.LandDetail!;
        Assert.Equal(12_000m,    ld.UnitPrice);       // FinalValueAdjusted (the adjusted unit price)
        Assert.Equal(500_000m,   ld.BuildingValue);    // PricingFinalValue.BuildingValue
        Assert.Equal(1_500_000m, ld.AppraisalValue);  // PricingFinalValue.AppraisalPrice
    }

    // -----------------------------------------------------------------------
    // PR8-2: Cost approach, multi-title group — UnitPrice on all masters, BuildingCost+AppraisalValue on IsMaster only
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR8_2_CostApproach_MultiTitleGroup_UnitPriceOnAllAliases()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var masterTitle = $"PR8-2M-{tag}";
        var alias1Title = $"PR8-2A1-{tag}";
        var alias2Title = $"PR8-2A2-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);

            var a = CreateAppraisalSeed(Guid.NewGuid());

            // One property with three titles → IsMaster + 2 aliases
            var prop = a.AddLandProperty();
            prop.LandDetail!.Update(address: Address.Create("S1", "D1", "BKK"), landOffice: "LO-PR8");
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, masterTitle, "Chanote"));
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, alias1Title, "Chanote"));
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, alias2Title, "Chanote"));

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;

            var group = a.CreateGroup("Multi Group");
            group.AddProperty(prop.Id);

            var pa = SeedCostApproachPricing(
                propertyGroupId: group.Id,
                finalValueAdjusted: 8_000m,
                buildingCost: 300_000m,
                appraisalPrice: 800_000m);

            appraisalDb.PricingAnalyses.Add(pa);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var allMasters = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Where(m => m.LandDetail != null &&
                        (m.LandDetail.TitleNumber == masterTitle ||
                         m.LandDetail.TitleNumber == alias1Title ||
                         m.LandDetail.TitleNumber == alias2Title))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, allMasters.Count);

        var isMasterRow = allMasters.Single(m => m.IsMaster);
        var aliases = allMasters.Where(m => !m.IsMaster).ToList();
        Assert.Equal(2, aliases.Count);

        // IsMaster gets all three values
        Assert.Equal(8_000m,    isMasterRow.LandDetail!.UnitPrice);
        Assert.Equal(300_000m,  isMasterRow.LandDetail.BuildingValue);
        Assert.Equal(800_000m,  isMasterRow.LandDetail.AppraisalValue);

        // Aliases get UnitPrice only; BuildingCost + AppraisalValue must be null
        foreach (var alias in aliases)
        {
            Assert.Equal(8_000m, alias.LandDetail!.UnitPrice);
            Assert.Null(alias.LandDetail.BuildingValue);
            Assert.Null(alias.LandDetail.AppraisalValue);
        }
    }

    // -----------------------------------------------------------------------
    // PR8-3: Non-cost approach — UnitPrice null, AppraisalValue populated from AppraisalPrice
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR8_3_NonCostApproach_UnitPriceNull_AppraisalValuePopulated()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var titleNo = $"PR8-3-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);

            var a = CreateAppraisalSeed(Guid.NewGuid());
            var prop = SeedLandProperty(a, "LO-PR8", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;

            var group = a.CreateGroup("Market Group");
            group.AddProperty(prop.Id);

            // Market approach — no building cost, AppraisalPrice set
            var pa = SeedMarketApproachPricing(
                propertyGroupId: group.Id,
                appraisalPrice: 2_000_000m);

            appraisalDb.PricingAnalyses.Add(pa);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        Assert.True(master.IsMaster);

        var ld = master.LandDetail!;
        // Non-cost approach: UnitPrice must be null
        Assert.Null(ld.UnitPrice);
        // BuildingCost: null (no cost approach, HasBuildingCost = false)
        Assert.Null(ld.BuildingValue);
        // AppraisalValue: populated from AppraisalPrice on the market-approach FinalValue
        Assert.Equal(2_000_000m, ld.AppraisalValue);
    }

    // -----------------------------------------------------------------------
    // PR8-4: No PricingAnalysis at all — all three null, no exception
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR8_4_NoPricingAnalysis_AllNull_NoException()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var titleNo = $"PR8-4-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            // No PropertyGroup, no PricingAnalysis — ungrouped property
            SeedLandProperty(a, "LO-PR8", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;
        }

        // Must not throw even without any pricing data
        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstOrDefaultAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        Assert.NotNull(master);
        Assert.Null(master.LandDetail!.UnitPrice);
        Assert.Null(master.LandDetail.BuildingValue);
        Assert.Null(master.LandDetail.AppraisalValue);
    }

    // -----------------------------------------------------------------------
    // PR8-5: Snapshot reflects real pricing values at group level and per-property
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR8_5_SnapshotReflectsRealPricingValues()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var titleNo = $"PR8-5-{tag}";
        Guid appraisalId;

        using (var seedScope = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seedScope);

            var a = CreateAppraisalSeed(Guid.NewGuid());
            var prop = SeedLandProperty(a, "LO-PR8", "BKK", "D1", "S1", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            appraisalId = a.Id;

            var group = a.CreateGroup("Snap Group");
            group.AddProperty(prop.Id);

            var pa = SeedCostApproachPricing(
                propertyGroupId: group.Id,
                finalValueAdjusted: 5_000m,
                buildingCost: 200_000m,
                appraisalPrice: 700_000m);

            appraisalDb.PricingAnalyses.Add(pa);
            await appraisalDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        using var assertScope = CreateScope();
        var collateralDb = GetCollateralDbContext(assertScope);

        var master = await collateralDb.CollateralMasters
            .Include(m => m.LandDetail)
            .Include(m => m.Engagements)
            .FirstAsync(m => m.LandDetail != null && m.LandDetail.TitleNumber == titleNo,
                TestContext.Current.CancellationToken);

        var engagement = master.Engagements.Single();
        Assert.NotNull(engagement.Snapshot);

        using var doc = JsonDocument.Parse(engagement.Snapshot);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("groups", out var groupsEl), "Snapshot missing 'groups'");
        var firstGroup = groupsEl.EnumerateArray().First();

        // Group-level values — populated from IsMaster LandDetail after PR-8 wiring
        Assert.True(firstGroup.TryGetProperty("buildingCost", out var bcEl), "Group missing 'buildingCost'");
        Assert.True(firstGroup.TryGetProperty("appraisalValue", out var avEl), "Group missing 'appraisalValue'");
        Assert.Equal(200_000m, bcEl.GetDecimal());
        Assert.Equal(700_000m, avEl.GetDecimal());

        // Per-property unitPrice — cost approach, so must be populated
        var firstProp = firstGroup.GetProperty("properties").EnumerateArray().First();
        Assert.True(firstProp.TryGetProperty("unitPrice", out var upEl), "Property entry missing 'unitPrice'");
        Assert.Equal(5_000m, upEl.GetDecimal());
    }
}
