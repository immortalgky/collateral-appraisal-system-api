using Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;
using Appraisal.Application.Features.PricingAnalysis.SetFinalValue;
using Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;
using Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;
using Appraisal.Application.Features.PricingAnalysis.UpdateMethod;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
using Appraisal.Domain.Services;
using NSubstitute;
using Shared.Data;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// The "land rate x land area = land" entry is open to a Market method the appraiser tagged
/// Role = Land, and only to that: LandAndBuilding / no role (a whole lump) and Income / Residual
/// are rejected.
/// </summary>
public class SetManualCostBreakdownMarketTests
{
    private static readonly Guid GroupId = Guid.NewGuid();

    private static (PricingAnalysis Pa, PricingAnalysisMethod Method, SetManualCostBreakdownCommandHandler Handler)
        Build(string approachType, string? role, decimal? landArea = 510m)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(GroupId);
        var approach = pa.AddApproach(approachType);
        var method = approach.AddMethod("WQS");
        if (role is not null)
            approach.SetMethodRole(method.Id, role);

        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);

        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        data.GetTotalLandAreaFromTitlesAsync(GroupId, Arg.Any<CancellationToken>()).Returns(landArea);

        return (pa, method, new SetManualCostBreakdownCommandHandler(repo, data));
    }

    [Fact]
    public async Task Market_Land_method_records_rate_area_and_takes_land_value_from_the_indicated_value()
    {
        var (pa, method, handler) = Build("Market", "Land");

        // 510 x 85,500 = 43,605,000 -> the card's rounded-to-thousand price is 44,000,000 when typed.
        var result = await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, 44_000_000m), CancellationToken.None);

        var fv = method.FinalValue!;
        Assert.Equal("Land", method.Role);
        Assert.Equal(85_500m, fv.FinalValueOverride);
        Assert.Equal(510m, fv.LandArea);
        Assert.Equal(44_000_000m, fv.IndicatedValue);
        Assert.Equal(44_000_000m, fv.LandValue);
        Assert.Equal(44_000_000m, method.MethodValue);
        Assert.Equal("PerSqWa", method.UnitType);
        Assert.Equal(43_605_000m, result.ComputedTotal);
        Assert.False(method.UseSystemCalc);
    }

    [Fact]
    public async Task Market_Land_method_without_a_typed_price_rounds_the_land_total_to_the_thousand()
    {
        var (pa, method, handler) = Build("Market", "Land");

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_501.5m, null), CancellationToken.None);

        // 510 x 85,501.5 = 43,605,765 whole baht -> nearest 1,000 = 43,606,000, same as Cost.
        Assert.Equal(43_606_000m, method.FinalValue!.IndicatedValue);
        Assert.Equal(43_606_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public async Task Clearing_the_rate_on_a_Market_Land_method_keeps_its_role_and_drops_the_breakdown()
    {
        var (pa, method, handler) = Build("Market", "Land");
        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, 44_000_000m), CancellationToken.None);

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, null, 44_000_000m), CancellationToken.None);

        Assert.Equal("Land", method.Role);
        Assert.Null(method.FinalValue);
        Assert.Equal("PerUnit", method.UnitType);
    }

    [Theory]
    [InlineData("Market", "LandAndBuilding")]
    [InlineData("Income", null)]
    [InlineData("Residual", null)]
    public async Task Other_non_cost_methods_are_rejected(string approachType, string? role)
    {
        var (pa, method, handler) = Build(approachType, role);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, 44_000_000m), CancellationToken.None));
        Assert.Null(method.FinalValue);
    }

    [Fact]
    public async Task Cost_Land_method_is_still_accepted()
    {
        var (pa, method, handler) = Build("Cost", "Land");

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, 44_000_000m), CancellationToken.None);

        Assert.Equal(44_000_000m, method.FinalValue!.LandValue);
    }

    // ── Cost + LandAndBuilding: land rate AND a keyed building value ─────────────────────────────

    [Fact]
    public async Task Cost_LandAndBuilding_stores_land_building_and_total_and_keeps_its_role()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");

        // land 510 x 85,500 = 43,605,000; + building 2,000,000 = 45,605,000 (already a thousand).
        var result = await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, null, 2_000_000m), CancellationToken.None);

        var fv = method.FinalValue!;
        Assert.Equal("LandAndBuilding", method.Role);
        Assert.Equal(85_500m, fv.FinalValueOverride);
        Assert.Equal(510m, fv.LandArea);
        Assert.Equal(43_605_000m, fv.LandValue);
        Assert.True(fv.HasBuildingValue);
        Assert.Equal(2_000_000m, fv.BuildingValue);
        Assert.Equal(45_605_000m, fv.IndicatedValue);
        Assert.Equal(45_605_000m, method.MethodValue);
        Assert.Equal("PerSqWa", method.UnitType);
        Assert.Equal(2_000_000m, result.BuildingValue);
        Assert.Equal(45_605_000m, result.ComputedTotal);
    }

    [Fact]
    public async Task Cost_LandAndBuilding_rounds_the_default_total_to_the_thousand()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");

        // 510 x 85,501.5 = 43,605,765; + 2,000,100 = 45,605,865 -> 45,606,000.
        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_501.5m, null, 2_000_100m), CancellationToken.None);

        Assert.Equal(45_606_000m, method.FinalValue!.IndicatedValue);
        Assert.Equal(43_605_765m, method.FinalValue.LandValue);
    }

    [Fact]
    public async Task Cost_LandAndBuilding_land_value_is_not_overwritten_by_the_typed_total()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, 46_000_000m, 2_000_000m), CancellationToken.None);

        Assert.Equal(46_000_000m, method.FinalValue!.IndicatedValue);
        Assert.Equal(43_605_000m, method.FinalValue.LandValue);   // land only, not the total
        Assert.Equal(46_000_000m, method.MethodValue);
    }

    [Fact]
    public async Task Cost_LandAndBuilding_without_a_building_value_is_rejected()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, null), CancellationToken.None));
        Assert.Null(method.FinalValue);
    }

    [Fact]
    public async Task Cost_LandAndBuilding_rejects_a_negative_building_value()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, null, -1m), CancellationToken.None));
    }

    [Fact]
    public async Task Clearing_the_rate_on_a_Cost_LandAndBuilding_method_keeps_its_role_and_the_lump()
    {
        var (pa, method, handler) = Build("Cost", "LandAndBuilding");
        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, null, 2_000_000m), CancellationToken.None);

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, null, 45_605_000m), CancellationToken.None);

        Assert.Equal("LandAndBuilding", method.Role);
        Assert.Null(method.FinalValue);
        Assert.Equal(45_605_000m, method.MethodValue);
        Assert.Equal("PerUnit", method.UnitType);
    }

    [Fact]
    public async Task A_linked_LandAndBuilding_method_stays_linked_and_its_BuildingCost_stays_deselected()
    {
        var (pa, method, handler) = Build("Cost", "Land");
        var cost = pa.Approaches.First();
        cost.SelectMethod(method.Id);
        var bc = cost.LinkOrCreateBuildingCostMethod(method.Id, 3_000_000m);   // calculated include-building path
        Assert.Equal("LandAndBuilding", method.Role);
        Assert.False(bc.IsSelected);

        await handler.Handle(
            new SetManualCostBreakdownCommand(pa.Id, method.Id, 85_500m, null, 2_000_000m), CancellationToken.None);

        Assert.Equal("LandAndBuilding", method.Role);
        Assert.Equal(bc.Id, method.LinkedMethodId);
        Assert.False(bc.IsSelected);
        Assert.Equal(2_000_000m, method.FinalValue!.BuildingValue);   // the keyed value, not the 3,000,000 snapshot
    }

    [Fact]
    public void Selecting_BuildingCost_next_to_a_selected_LandAndBuilding_method_is_still_rejected()
    {
        var (pa, method, _) = Build("Cost", "LandAndBuilding");
        var cost = pa.Approaches.First();
        cost.SelectMethod(method.Id);
        var bc = cost.AddMethod("BuildingCost", "Alternative");

        Assert.Throws<DomainException>(() => cost.SelectMethod(bc.Id));
    }

    // ── UpdateFinalValue on a Role=Land lumpsum: this save's IndicatedValue, not the previous one ──

    private static (PricingAnalysis Pa, PricingAnalysisMethod Method, UpdateFinalValueCommandHandler Handler)
        BuildLumpForUpdate(bool withBuilding)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(GroupId);
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("WQS");
        method.SetValue(1_000_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_000_000m));
        method.FinalValue!.SetIndicatedValue(1_000_000m);   // the PREVIOUS save's figure
        if (withBuilding)
            method.FinalValue.SetBuildingValue(2_000_000m);

        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        data.GetTotalLandAreaFromTitlesAsync(GroupId, Arg.Any<CancellationToken>()).Returns(510m);
        return (pa, method, new UpdateFinalValueCommandHandler(repo, data));
    }

    private static UpdateFinalValueCommand UpdateCommand(
        PricingAnalysis pa, PricingAnalysisMethod method, decimal indicated, bool? hasBuilding = null) =>
        new(pa.Id, method.FinalValue!.Id, indicated, IncludeLandArea: false, LandArea: null, LandValue: null,
            HasBuildingValue: hasBuilding, BuildingValue: null, IndicatedValue: indicated);

    [Fact]
    public async Task UpdateFinalValue_on_a_Land_lumpsum_writes_the_new_indicated_value_as_land()
    {
        var (pa, method, handler) = BuildLumpForUpdate(withBuilding: false);
        method.SetRole("Land");

        await handler.Handle(UpdateCommand(pa, method, 3_000_000m), CancellationToken.None);

        Assert.Equal(510m, method.FinalValue!.LandArea);
        Assert.Equal(3_000_000m, method.FinalValue.LandValue);   // not the previous 1,000,000
        Assert.True(method.FinalValue.IncludeLandArea);
    }

    [Fact]
    public async Task UpdateFinalValue_on_a_Land_lumpsum_that_just_had_its_building_unticked_clears_the_stale_land_figures()
    {
        var (pa, method, handler) = BuildLumpForUpdate(withBuilding: true);
        method.SetRole("Land");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);

        await handler.Handle(UpdateCommand(pa, method, 5_000_000m, hasBuilding: false), CancellationToken.None);

        // 5,000,000 is the echoed land+building total, so it is not land; the stale 750,000 goes too.
        Assert.Null(method.FinalValue.LandValue);
        Assert.Null(method.FinalValue.LandArea);
    }

    [Fact]
    public async Task UpdateFinalValue_on_a_group_with_no_land_properties_and_false_excludes_land()
    {
        // GetTotalLandAreaFromTitlesAsync returns null for a condo / building / machinery group; that is
        // a known 0 for a property-group analysis, not "no group".
        var (pa, method, handler) = BuildLumpForUpdate(withBuilding: false);
        method.SetRole("LandAndBuilding");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        data.GetTotalLandAreaFromTitlesAsync(GroupId, Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        handler = new UpdateFinalValueCommandHandler(repo, data);

        await handler.Handle(UpdateCommand(pa, method, 3_000_000m), CancellationToken.None);   // IncludeLandArea: false

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public async Task UpdateFinalValue_on_a_project_model_honours_false_like_any_known_area()
    {
        var pa = PricingAnalysis.CreateForProjectModel(Guid.NewGuid());
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("WQS");
        method.SetValue(1_000_000m, 20_000m, "PerSqWa");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_000_000m));
        method.FinalValue!.SetLandAreaValues(100m, 2_000_000m);
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        var handler = new UpdateFinalValueCommandHandler(repo, data);

        await handler.Handle(UpdateCommand(pa, method, 3_000_000m), CancellationToken.None);   // IncludeLandArea: false

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public async Task UpdateFinalValue_on_a_reference_analysis_keeps_its_land_override_even_when_false_is_sent()
    {
        var src = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var source = src.AddApproach("Cost").AddMethod("WQS");
        source.SetValue(1_000_000m, null, "PerUnit");
        source.SetFinalValue(PricingFinalValue.Create(source.Id, 1_000_000m));
        source.FinalValue!.SetLandAreaValues(100m, 30_000_000m);
        var pa = PricingAnalysis.CreateReferenceFromMethod(
            PricingAnalysisSubjectType.MachineryCostRef, Guid.NewGuid(), null, source, landAreaOverride: 250m);
        var method = pa.Approaches.Single().Methods.Single();
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        var handler = new UpdateFinalValueCommandHandler(repo, data);

        await handler.Handle(UpdateCommand(pa, method, 3_000_000m), CancellationToken.None);   // IncludeLandArea: false

        Assert.True(method.FinalValue!.IncludeLandArea);
        Assert.Equal(250m, method.FinalValue.LandArea);
        Assert.Equal(30_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public async Task UpdateMethod_rejects_a_role_change_on_a_reference_analysis()
    {
        var src = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var source = src.AddApproach("Cost").AddMethod("WQS");
        var pa = PricingAnalysis.CreateReferenceFromMethod(
            PricingAnalysisSubjectType.MachineryCostRef, Guid.NewGuid(), null, source);
        var method = pa.Approaches.Single().Methods.Single();
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var handler = new UpdateMethodCommandHandler(repo);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateMethodCommand(pa.Id, method.Id, Role: "Land"), CancellationToken.None));
        Assert.Null(method.Role);
    }

    [Fact]
    public async Task UpdateMethod_still_allows_a_Cost_role_on_a_reference_analysis()
    {
        var src = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var source = src.AddApproach("Cost").AddMethod("WQS");
        var pa = PricingAnalysis.CreateReferenceFromMethod(
            PricingAnalysisSubjectType.MachineryCostRef, Guid.NewGuid(), null, source);
        var cost = pa.AddApproach("Cost");
        var method = cost.AddMethod("WQS");
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var handler = new UpdateMethodCommandHandler(repo);

        await handler.Handle(new UpdateMethodCommand(pa.Id, method.Id, Role: "LandAndBuilding"), CancellationToken.None);

        Assert.Equal("LandAndBuilding", method.Role);
    }

    // ── IndicatedValue of 0 or less on a Role=Land row: every handler ends with cleared land figures ──

    [Fact]
    public async Task SetFinalValue_with_a_zero_indicated_value_clears_the_land_figures_of_a_Land_lump()
    {
        var (pa, method, _) = BuildLumpForUpdate(withBuilding: false);
        method.SetRole("Land");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        data.GetTotalLandAreaFromTitlesAsync(GroupId, Arg.Any<CancellationToken>()).Returns(510m);
        var handler = new SetFinalValueCommandHandler(repo, data);

        await handler.Handle(
            new SetFinalValueCommand(pa.Id, method.Id, 0m, null, null, null, null, null, IndicatedValue: 0m),
            CancellationToken.None);

        Assert.Null(method.FinalValue.LandValue);
        Assert.Null(method.FinalValue.LandArea);
        Assert.True(method.FinalValue.IncludeLandArea);   // flag untouched
    }

    [Fact]
    public async Task SaveComparativeAnalysis_with_a_zero_indicated_value_clears_the_land_figures_of_a_Land_lump()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(GroupId);
        var approach = pa.AddApproach("Cost");
        var method = approach.AddMethod("Leasehold");   // no calculation service: only the land logic runs
        method.SetValue(1_000_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_000_000m));
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);
        var repo = Substitute.For<IPricingAnalysisRepository>();
        repo.GetByIdWithAllDataAsync(pa.Id, Arg.Any<CancellationToken>()).Returns(pa);
        var data = Substitute.For<PricingPropertyDataService>(
            Substitute.For<IAppraisalRepository>(), Substitute.For<ISqlConnectionFactory>());
        data.GetTotalLandAreaFromTitlesAsync(GroupId, Arg.Any<CancellationToken>()).Returns(510m);
        var handler = new SaveComparativeAnalysisCommandHandler(
            repo, Substitute.For<IMarketComparableRepository>(),
            new PricingCalculationServiceResolver(new IncomeCalculationService()), data);

        await handler.Handle(
            new SaveComparativeAnalysisCommand(pa.Id, method.Id, [], [], [], IndicatedValue: 0m),
            CancellationToken.None);

        Assert.Null(method.FinalValue.LandValue);
        Assert.Null(method.FinalValue.LandArea);
        Assert.True(method.FinalValue.IncludeLandArea);
    }
}
