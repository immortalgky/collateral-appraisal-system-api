using Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using NSubstitute;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Tests for the PricingFinalValue consolidation (Phase B2/B3).
///
/// Coverage:
/// 1. PricingFinalValue entity — Create, UpdateFinalValue, BuildingValue/HasBuildingValue
///    rename sanity (formerly BuildingCost/HasBuildingCost), CloneForMethod round-trip.
/// 2. Mirror-to-PricingFinalValue domain simulation — first save creates row, second save
///    updates in place; this pattern is shared by all 5 handlers (Income, Leasehold,
///    ProfitRent, Hypothesis, MachineryCost).
/// 3. SaveMachineCostItemsCommandHandler — handler-level test using NSubstitute;
///    asserts PricingFinalValue row is created/updated with the expected sum-of-FMV values.
/// 4. PricingAnalysis.SetFinalValues rollup — the PricingFinalValue write is additive;
///    FinalAppraisedValue derives from MethodValue, never from PricingFinalValue fields.
///
/// NOT COVERED — BackfillPricingFinalValues migration SQL:
///   The test project references Microsoft.EntityFrameworkCore.InMemory which does not
///   execute raw SQL strings passed to migrationBuilder.Sql. Testing the idempotency
///   guards (NOT EXISTS) and the second-run zero-insert behaviour requires a real
///   SQL Server instance. See Tests/Integration/Collateral.Integration.Tests/ for the
///   established pattern of spinning up a testcontainer or localdb for such cases.
/// </summary>
public class PricingFinalValueConsolidationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Section 1 — PricingFinalValue entity (pure domain, no EF or DB)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Sets_FinalValue_And_FinalValueRounded()
    {
        var methodId = Guid.NewGuid();

        var pfv = PricingFinalValue.Create(methodId, 1_000_000m, 1_000_000m);

        Assert.Equal(methodId, pfv.PricingMethodId);
        Assert.Equal(1_000_000m, pfv.FinalValue);
        Assert.Equal(1_000_000m, pfv.FinalValueRounded);
    }

    [Fact]
    public void Create_DefaultsTo_IncludeLandArea_True_And_HasBuildingValue_False()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 500_000m, 500_000m);

        Assert.True(pfv.IncludeLandArea);
        Assert.False(pfv.HasBuildingValue);
        Assert.Null(pfv.BuildingValue);
        Assert.Null(pfv.FinalValueAdjusted);
        Assert.Null(pfv.AppraisalPrice);
    }

    [Fact]
    public void UpdateFinalValue_Overwrites_Both_Fields()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 100m, 100m);

        pfv.UpdateFinalValue(200m, 200m);

        Assert.Equal(200m, pfv.FinalValue);
        Assert.Equal(200m, pfv.FinalValueRounded);
    }

    // BuildingValue/HasBuildingValue rename sanity (formerly BuildingCost/HasBuildingCost in Phase B1)
    [Fact]
    public void SetBuildingValue_Sets_HasBuildingValue_True_And_Stores_Amount()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 500_000m, 500_000m);

        pfv.SetBuildingValue(150_000m);

        Assert.True(pfv.HasBuildingValue);
        Assert.Equal(150_000m, pfv.BuildingValue);
    }

    [Fact]
    public void ClearBuildingValue_Sets_HasBuildingValue_False_And_Clears_Amount()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 500_000m, 500_000m);
        pfv.SetBuildingValue(150_000m);

        pfv.ClearBuildingValue();

        Assert.False(pfv.HasBuildingValue);
        Assert.Null(pfv.BuildingValue);
    }

    [Fact]
    public void SetFinalValueAdjusted_Stores_Value()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 500_000m, 500_000m);

        pfv.SetFinalValueAdjusted(480_000m);

        Assert.Equal(480_000m, pfv.FinalValueAdjusted);
    }

    [Fact]
    public void SetAppraisalPrice_Stores_Value()
    {
        var pfv = PricingFinalValue.Create(Guid.NewGuid(), 500_000m, 500_000m);

        pfv.SetAppraisalPrice(490_000m);

        Assert.Equal(490_000m, pfv.AppraisalPrice);
    }

    /// <summary>
    /// CloneForMethod deep-copies BuildingValue and HasBuildingValue — round-trip sanity
    /// for the Phase B1 rename from BuildingCost/HasBuildingCost.
    /// </summary>
    [Fact]
    public void CloneForMethod_DeepCopies_BuildingValue_HasBuildingValue_RoundTrip()
    {
        var source = PricingFinalValue.Create(Guid.NewGuid(), 800_000m, 800_000m);
        source.SetBuildingValue(250_000m);
        source.SetFinalValueAdjusted(750_000m);
        source.SetAppraisalPrice(760_000m);

        var newMethodId = Guid.NewGuid();
        var clone = PricingFinalValue.CloneForMethod(source, newMethodId);

        Assert.Equal(newMethodId, clone.PricingMethodId);
        Assert.Equal(800_000m, clone.FinalValue);
        Assert.Equal(800_000m, clone.FinalValueRounded);
        Assert.True(clone.HasBuildingValue);
        Assert.Equal(250_000m, clone.BuildingValue);
        Assert.Equal(750_000m, clone.FinalValueAdjusted);
        Assert.Equal(760_000m, clone.AppraisalPrice);
    }

    [Fact]
    public void CloneForMethod_WithoutBuildingValue_Clones_False_Null()
    {
        var source = PricingFinalValue.Create(Guid.NewGuid(), 300_000m, 300_000m);
        // no SetBuildingValue call

        var clone = PricingFinalValue.CloneForMethod(source, Guid.NewGuid());

        Assert.False(clone.HasBuildingValue);
        Assert.Null(clone.BuildingValue);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section 2 — Mirror-to-PricingFinalValue domain simulation
    //
    // Each of the 5 handlers (Income, Leasehold, ProfitRent, Hypothesis,
    // MachineryCost) follows this pattern on the method object:
    //   if (method.FinalValue is null) method.SetFinalValue(PricingFinalValue.Create(...))
    //   else                           method.FinalValue.UpdateFinalValue(...)
    // These tests verify the domain API works for all method types.
    // ─────────────────────────────────────────────────────────────────────────

    private static PricingAnalysisMethod BuildMethod(string methodType)
    {
        var approach = PricingAnalysisApproach.Create(Guid.NewGuid(), "Cost");
        return approach.AddMethod(methodType);
    }

    [Fact]
    public void Mirror_FirstSave_NullFinalValue_Creates_PricingFinalValueRow()
    {
        var method = BuildMethod("MachineryCost");
        Assert.Null(method.FinalValue); // pre-condition

        const decimal value = 2_500_000m;

        // Handler pattern: create on first save
        method.SetFinalValue(PricingFinalValue.Create(method.Id, value, value));

        Assert.NotNull(method.FinalValue);
        Assert.Equal(value, method.FinalValue.FinalValue);
        Assert.Equal(value, method.FinalValue.FinalValueRounded);
        Assert.Equal(method.Id, method.FinalValue.PricingMethodId);
    }

    [Fact]
    public void Mirror_SecondSave_ExistingRow_UpdatesInPlace_RowIdStable()
    {
        var method = BuildMethod("MachineryCost");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_000_000m, 1_000_000m));
        var originalRowId = method.FinalValue!.Id;

        // Handler pattern: update in place on subsequent saves
        method.FinalValue.UpdateFinalValue(1_500_000m, 1_500_000m);

        Assert.Equal(originalRowId, method.FinalValue.Id); // same row, not a new Create
        Assert.Equal(1_500_000m, method.FinalValue.FinalValue);
        Assert.Equal(1_500_000m, method.FinalValue.FinalValueRounded);
    }

    [Theory]
    [InlineData("Income")]
    [InlineData("Leasehold")]
    [InlineData("ProfitRent")]
    [InlineData("Hypothesis")]
    [InlineData("MachineryCost")]
    [InlineData("BuildingCost")]
    public void Mirror_AllMethodTypes_CanCreate_PricingFinalValueRow(string methodType)
    {
        var method = BuildMethod(methodType);

        const decimal val = 3_000_000m;
        method.SetFinalValue(PricingFinalValue.Create(method.Id, val, val));

        Assert.NotNull(method.FinalValue);
        Assert.Equal(method.Id, method.FinalValue.PricingMethodId);
        Assert.Equal(val, method.FinalValue.FinalValue);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section 3 — PricingAnalysis.SetFinalValues rollup
    //
    // PricingFinalValue is additive: writing it must NOT change how the
    // FinalAppraisedValue rollup is computed. Rollup derives from
    // approach.ApproachValue (== MethodValue), never from PricingFinalValue fields.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetFinalValues_Rollup_Unchanged_After_PricingFinalValue_Write()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("MachineryCost");

        // Simulate handler: set method value + mirror to PricingFinalValue
        method.SetValue(4_000_000m);
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 4_000_000m, 4_000_000m));

        approach.SetValue(method.MethodValue!.Value);
        pa.SetFinalValues(approach.ApproachValue!.Value);

        Assert.Equal(4_000_000m, pa.FinalAppraisedValue);
    }

    [Fact]
    public void SetFinalValues_Rollup_EqualsMethodValue_Not_PricingFinalValueField()
    {
        // When the user-rounded PricingFinalValue differs from MethodValue,
        // FinalAppraisedValue must track MethodValue (the canonical commit value),
        // not the user-side rounding in PricingFinalValue.FinalValueRounded.
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("MachineryCost");

        const decimal methodVal = 5_000_000m;
        const decimal userRounded = 4_950_000m; // different, user-edited

        method.SetValue(methodVal);
        method.SetFinalValue(PricingFinalValue.Create(method.Id, userRounded, userRounded));

        approach.SetValue(method.MethodValue!.Value);
        pa.SetFinalValues(approach.ApproachValue!.Value);

        Assert.Equal(methodVal, pa.FinalAppraisedValue);     // rollup == MethodValue
        Assert.Equal(userRounded, method.FinalValue!.FinalValueRounded); // PFV row unaffected
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Section 4 — SaveMachineCostItemsCommandHandler (handler-level)
    //
    // Uses NSubstitute to mock IPricingAnalysisRepository. The handler is the
    // simplest of the 5 handlers: no extra calculation services with complex
    // dependencies, no DB flush for ID generation, no ISender/ISqlConnection.
    // ─────────────────────────────────────────────────────────────────────────

    private static (PricingAnalysis pa, PricingAnalysisMethod method)
        BuildMachineryAnalysis()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("MachineryCost");
        return (pa, method);
    }

    /// <summary>
    /// Builds a MachineCostItemInput with all required fields set so that
    /// MachineryCostCalculationService.Recalculate preserves the FairMarketValue
    /// (it only nulls FMV when RcnReplacementCost or LifeSpanYears is null/zero).
    /// </summary>
    private static MachineCostItemInput ValidItem(
        Guid appraisalPropertyId,
        decimal fairMarketValue,
        Guid? existingId = null) =>
        new MachineCostItemInput(
            Id: existingId,
            AppraisalPropertyId: appraisalPropertyId,
            DisplaySequence: 0,
            RcnReplacementCost: fairMarketValue,
            LifeSpanYears: 10m,   // non-null, non-zero → Recalculate keeps FMV
            ConditionFactor: 0m,
            FunctionalObsolescence: 1m,
            EconomicObsolescence: 1m,
            FairMarketValue: fairMarketValue,
            MarketDemandAvailable: true,
            Notes: null
        );

    private SaveMachineCostItemsCommandHandler BuildHandler(PricingAnalysis pa)
    {
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>())
            .Returns(pa);
        var resolver = new PricingCalculationServiceResolver(new IncomeCalculationService());
        return new SaveMachineCostItemsCommandHandler(repo, resolver);
    }

    [Fact]
    public async Task SaveMachineCostItems_FirstSave_CreatesPricingFinalValue_WithSumOfFmv()
    {
        var (pa, method) = BuildMachineryAnalysis();
        var handler = BuildHandler(pa);

        var propA = Guid.NewGuid();
        var propB = Guid.NewGuid();
        var command = new SaveMachineCostItemsCommand(
            PricingAnalysisId: pa.Id,
            MethodId: method.Id,
            Items: [ValidItem(propA, 1_000_000m), ValidItem(propB, 500_000m)],
            Remark: null
        );

        await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(method.FinalValue);
        Assert.Equal(1_500_000m, method.FinalValue.FinalValue);
        Assert.Equal(1_500_000m, method.FinalValue.FinalValueRounded);
        Assert.Equal(method.Id, method.FinalValue.PricingMethodId);
    }

    [Fact]
    public async Task SaveMachineCostItems_SecondSave_UpdatesPricingFinalValue_InPlace()
    {
        var (pa, method) = BuildMachineryAnalysis();

        // Simulate prior save: row already exists
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_500_000m, 1_500_000m));
        var existingRowId = method.FinalValue!.Id;

        var handler = BuildHandler(pa);

        var propA = Guid.NewGuid();
        var command = new SaveMachineCostItemsCommand(
            PricingAnalysisId: pa.Id,
            MethodId: method.Id,
            Items: [ValidItem(propA, 2_000_000m)],
            Remark: null
        );

        await handler.Handle(command, CancellationToken.None);

        // Row must be updated in-place (same Id), not replaced with a new Create
        Assert.Equal(existingRowId, method.FinalValue!.Id);
        Assert.Equal(2_000_000m, method.FinalValue.FinalValue);
        Assert.Equal(2_000_000m, method.FinalValue.FinalValueRounded);
    }

    [Fact]
    public async Task SaveMachineCostItems_StoresFinalValueAdjusted_And_AppraisalPrice()
    {
        var (pa, method) = BuildMachineryAnalysis();
        var handler = BuildHandler(pa);

        var propA = Guid.NewGuid();
        var command = new SaveMachineCostItemsCommand(
            PricingAnalysisId: pa.Id,
            MethodId: method.Id,
            Items: [ValidItem(propA, 800_000m)],
            Remark: null,
            FinalValueAdjusted: 780_000m,
            AppraisalPrice: 790_000m
        );

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(780_000m, method.FinalValue!.FinalValueAdjusted);
        Assert.Equal(790_000m, method.FinalValue.AppraisalPrice);
    }

    [Fact]
    public async Task SaveMachineCostItems_EmptyItems_CreatesPricingFinalValueWithZero()
    {
        var (pa, method) = BuildMachineryAnalysis();
        var handler = BuildHandler(pa);

        var command = new SaveMachineCostItemsCommand(
            PricingAnalysisId: pa.Id,
            MethodId: method.Id,
            Items: [],
            Remark: null
        );

        await handler.Handle(command, CancellationToken.None);

        // Even with no items the row is created (with totalFmv = 0)
        Assert.NotNull(method.FinalValue);
        Assert.Equal(0m, method.FinalValue.FinalValue);
        Assert.Equal(0m, method.FinalValue.FinalValueRounded);
    }
}
