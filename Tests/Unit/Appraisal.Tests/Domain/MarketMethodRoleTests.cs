using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// The appraiser declares what a Market method values with the same Role chip the Cost approach has:
/// Land (the land alone) or LandAndBuilding (the whole property as one lump). Income and Residual
/// take no role.
/// </summary>
public class MarketMethodRoleTests
{
    private static (PricingAnalysisApproach Approach, PricingAnalysisMethod Method) With(string approachType)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var approach = pa.AddApproach(approachType);
        return (approach, approach.AddMethod("WQS"));
    }

    [Theory]
    [InlineData("Land")]
    [InlineData("LandAndBuilding")]
    public void Market_accepts_Land_and_LandAndBuilding(string role)
    {
        var (market, method) = With("Market");

        market.SetMethodRole(method.Id, role);

        Assert.Equal(role, method.Role);
    }

    [Theory]
    [InlineData("Building")]
    [InlineData("Machinery")]
    [InlineData("Nonsense")]
    public void Market_rejects_any_other_role(string role)
    {
        var (market, method) = With("Market");

        Assert.Throws<DomainException>(() => market.SetMethodRole(method.Id, role));
    }

    [Theory]
    [InlineData("Income")]
    [InlineData("Residual")]
    public void Income_and_Residual_reject_a_role(string approachType)
    {
        var (approach, method) = With(approachType);

        Assert.Throws<DomainException>(() => approach.SetMethodRole(method.Id, "Land"));
    }

    [Fact]
    public void Market_role_does_not_trip_the_Cost_double_count_rule()
    {
        // Two selected Market methods may both be Land / LandAndBuilding: the one-per-component rule
        // belongs to the Cost rollup.
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var market = pa.AddApproach("Market");
        var a = market.AddMethod("WQS");
        var b = market.AddMethod("SaleGrid");

        market.SetMethodRole(a.Id, "LandAndBuilding");
        market.SetMethodRole(b.Id, "LandAndBuilding");

        Assert.Equal("LandAndBuilding", a.Role);
        Assert.Equal("LandAndBuilding", b.Role);
    }

    [Fact]
    public void Changing_a_market_method_role_leaves_its_land_figures_for_the_next_price_save()
    {
        var (market, method) = With("Market");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 1_000m));
        market.SetMethodRole(method.Id, "Land");
        method.FinalValue!.SetLandAreaValues(510m, 43_605_000m);

        market.SetMethodRole(method.Id, "LandAndBuilding");

        // Same as Cost: ApplyLandAreaValue settles the figures on the next price save.
        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(43_605_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_approach_still_accepts_its_own_roles()
    {
        var (cost, method) = With("Cost");

        cost.SetMethodRole(method.Id, "Land");

        Assert.Equal("Land", method.Role);
    }

    [Theory]
    [InlineData(new[] { "L" }, "Land")]
    [InlineData(new[] { "L", "L" }, "Land")]
    [InlineData(new[] { "LSL" }, "Land")]
    [InlineData(new[] { "L", "LSL" }, "Land")]
    [InlineData(new[] { "L", "B" }, "LandAndBuilding")]
    [InlineData(new[] { "LB" }, "LandAndBuilding")]
    [InlineData(new[] { "LSB" }, "LandAndBuilding")]
    [InlineData(new[] { "LS" }, "LandAndBuilding")]
    [InlineData(new[] { "U" }, "LandAndBuilding")]
    [InlineData(new[] { "LSU" }, "LandAndBuilding")]
    [InlineData(new[] { "MAC" }, "LandAndBuilding")]
    [InlineData(new[] { "VEH" }, "LandAndBuilding")]
    [InlineData(new[] { "VES" }, "LandAndBuilding")]
    [InlineData(new string[0], "LandAndBuilding")]
    public void Default_market_role_is_Land_only_for_a_group_of_land_properties(string[] types, string expected)
    {
        Assert.Equal(expected, PricingPropertyDataService.DefaultMarketRole(types));
    }

    [Fact]
    public void RevertToLand_is_a_no_op_on_a_Market_approach()
    {
        // ResetPricingMethod calls it for every approach; a Market LandAndBuilding is the appraiser's
        // own declaration and must survive.
        var (market, method) = With("Market");
        market.SetMethodRole(method.Id, "LandAndBuilding");

        market.RevertToLand(method.Id);

        Assert.Equal("LandAndBuilding", method.Role);
    }

    [Theory]
    [InlineData("Land")]
    [InlineData("LandAndBuilding")]
    [InlineData("Building")]
    [InlineData("Machinery")]
    public void A_reference_clone_takes_no_role_from_its_Cost_source(string sourceRole)
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var cost = pa.AddApproach("Cost");
        var source = cost.AddMethod("WQS");
        source.SetRole(sourceRole);
        source.SetFinalValue(PricingFinalValue.Create(source.Id, 1_000m));
        source.FinalValue!.SetLandAreaValues(100m, 30_000_000m);

        var reference = PricingAnalysis.CreateReferenceFromMethod(
            PricingAnalysisSubjectType.MachineryCostRef, Guid.NewGuid(), null, source, landAreaOverride: 250m);

        var clone = reference.Approaches.Single().Methods.Single();
        Assert.Null(clone.Role);
        Assert.Equal(250m, clone.FinalValue!.LandArea);
        Assert.Equal(30_000_000m, clone.FinalValue.LandValue);
    }

    [Fact]
    public void A_reference_keeps_its_DCF_land_override_through_the_land_value_sync()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var cost = pa.AddApproach("Cost");
        var source = cost.AddMethod("WQS");
        source.SetFinalValue(PricingFinalValue.Create(source.Id, 1_000m));
        source.FinalValue!.SetLandAreaValues(100m, 30_000_000m);
        source.FinalValue.SetIndicatedValue(99_000_000m);
        var reference = PricingAnalysis.CreateReferenceFromMethod(
            PricingAnalysisSubjectType.MachineryCostRef, Guid.NewGuid(), null, source, landAreaOverride: 250m);
        var clone = reference.Approaches.Single().Methods.Single();

        clone.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(30_000_000m, clone.FinalValue!.LandValue);
    }

    [Theory]
    [InlineData("Market")]
    [InlineData("Income")]
    [InlineData("Cost")]
    public void RevertToLand_throws_for_an_unknown_method_on_every_approach(string approachType)
    {
        var (approach, _) = With(approachType);

        Assert.Throws<NotFoundException>(() => approach.RevertToLand(Guid.NewGuid()));
    }
}
