using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Guards who decides whether a land figure is recorded. The COST approach breaks a collateral into
/// named components, so a per-area unit there is a land valuation. Outside it the APPRAISER decides
/// through <c>Role</c>: a Market method tagged "Land" values the land alone and records
/// area x rate (then the indicated value, via the sync); "LandAndBuilding", a null Role, or "Land" on
/// a lumpsum unit is a whole lump and clears any land figure.
/// <para>
/// The gate takes the approach type as an argument rather than inferring it from <c>Role</c>. Role
/// is null on Income/Residual, but it is ALSO null on cost rows that predate it — so inferring
/// would have wiped a real land value off legacy data on the next save of any kind.
/// </para>
/// </summary>
public class ApplyLandAreaValueRoleGateTests
{
    private const decimal LandArea = 4_000m;
    private const decimal RatePerSqWa = 71_566.67m;
    private const decimal ExpectedLandValue = 286_266_680m; // 71,566.67 × 4,000

    private static PricingAnalysisMethod BuildRatePricedMethod(string? role)
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(RatePerSqWa, RatePerSqWa, "PerSqWa");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, RatePerSqWa));
        method.SetRole(role);
        return method;
    }

    [Fact]
    public void Cost_approach_method_still_records_land_area_and_value()
    {
        var method = BuildRatePricedMethod("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_cost_row_that_predates_Role_keeps_its_land_value()
    {
        // The regression the approach-type argument exists to prevent. This aggregate states in
        // several places that "older rows predate Role"; inferring market from a null Role would
        // have cleared their land figures on the next save, including one that never touched price.
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    private static PricingAnalysisMethod BuildLumpMethod(string? role)
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(6_796_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 6_796_000m));
        method.SetRole(role);
        return method;
    }

    [Fact]
    public void Market_Land_method_priced_per_area_records_land_area_and_value()
    {
        var method = BuildRatePricedMethod("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
        Assert.True(method.FinalValue.IncludeLandArea);
    }

    [Fact]
    public void Market_Land_method_priced_per_sqm_takes_the_lump_path_not_area_times_rate()
    {
        // PerSqm is quoted against the usable (building) area, so it is not a land rate: the land
        // figure is the method's own value, with the net title area beside it.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(RatePerSqWa, RatePerSqWa, "PerSqm");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, RatePerSqWa));
        method.SetRole("Land");
        method.FinalValue!.SetIndicatedValue(5_000_000m);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue.LandArea);
        Assert.Equal(5_000_000m, method.FinalValue.LandValue);   // not area x rate
    }

    [Fact]
    public void Market_Land_method_takes_the_override_rate_then_the_indicated_value()
    {
        // Appraisal 69105533, group 1: WQS, PerSqWa, override 85,500, titled area 510, and the
        // appraiser keyed an indicated value of 44,000,000 where area x rate gives 43,605,000.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "WQS");
        method.SetValue(80_000m, 80_000m, "PerSqWa");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 85_500m));
        method.SetRole("Land");
        method.FinalValue!.SetFinalValueOverride(85_500m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: true, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(43_605_000m, method.FinalValue.LandValue);

        method.FinalValue.SetIndicatedValue(44_000_000m);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(44_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_LandAndBuilding_method_priced_per_area_records_no_land_figures()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_method_with_no_Role_records_no_land_figures_despite_a_per_area_rate()
    {
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void An_explicitly_supplied_land_value_does_not_rescue_a_market_LandAndBuilding_method()
    {
        // There is no separable land figure to record on a lump-priced collateral, so a number the
        // client sends is refused rather than stored.
        var method = BuildRatePricedMethod("LandAndBuilding");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: 123_000_000m, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_Land_method_on_a_lumpsum_unit_records_the_whole_value_as_land()
    {
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetIndicatedValue(3_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);   // owns the value copy

        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(3_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_LandAndBuilding_method_on_a_lumpsum_unit_still_clears_a_stale_land_figure()
    {
        var method = BuildLumpMethod("LandAndBuilding");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_Land_method_on_a_lumpsum_unit_records_net_area_and_value_even_when_false_is_sent()
    {
        // Appraisal 69105533: Cost WQS, Role=Land, PerUnit, indicated 3,000,000. The price section
        // derives includeLandArea = isUnitPrice (false for a lump) and has no toggle, so that false
        // is not an exclusion. landAreaFromTitles is the NET area (deductions already taken off).
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetIndicatedValue(3_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(3_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_false_is_still_honoured_for_a_Land_method_priced_by_area()
    {
        var method = BuildRatePricedMethod("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue!.IncludeLandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_false_is_still_honoured_for_a_cost_lump_that_is_not_Land()
    {
        var method = BuildLumpMethod("LandAndBuilding");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue!.IncludeLandArea);
    }

    [Fact]
    public void Lump_Land_method_with_a_building_in_the_row_clears_stale_land_figures_and_keeps_the_flag()
    {
        // IndicatedValue / MethodValue is then a land+building total; it must never become LandValue,
        // and whatever an earlier save left must not survive as a land figure.
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);
        method.FinalValue.SetBuildingValue(2_000_000m);
        method.FinalValue.SetIndicatedValue(5_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
        Assert.True(method.FinalValue.IncludeLandArea);
    }

    [Fact]
    public void Lump_Land_method_whose_building_was_just_unticked_clears_stale_land_figures()
    {
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);
        method.FinalValue.SetIndicatedValue(5_000_000m);   // the echoed land+building total

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true,
            buildingWasPresentBeforeThisSave: true);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_with_no_indicated_value_uses_the_method_value_as_land()
    {
        var method = BuildLumpMethod("Land");   // MethodValue 6,796,000, no IndicatedValue

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(510m, method.FinalValue!.LandArea);
        Assert.Equal(6_796_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_lump_reference_with_no_titles_keeps_its_DCF_override_even_when_false_is_sent()
    {
        // A reference has no titles (area 0) and the price section sends includeLandArea = false for a
        // lump. The no-titles guard comes first, so neither the exclusion nor anything else runs.
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetLandAreaValues(250m, 30_000_000m);

        method.ApplyLandAreaValue(null, explicitLandValue: null, includeLandArea: false, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Equal(250m, method.FinalValue.LandArea);
        Assert.Equal(30_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_whose_building_was_present_before_this_save_never_gets_the_old_total()
    {
        // The building was just unticked (HasBuildingValue already cleared) but IndicatedValue is still
        // the echoed land+building total.
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetIndicatedValue(5_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true,
            buildingWasPresentBeforeThisSave: true);

        Assert.Null(method.FinalValue.LandValue);
    }

    private static PricingAnalysisMethod BuildForgottenUnitLandMethod()
    {
        // SetCalcMode -> ClearValue nulls the unit and excludes the land area.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        method.SetRole("Land");
        return method;
    }

    [Fact]
    public void A_Land_method_with_no_unit_still_honours_false()
    {
        var method = BuildForgottenUnitLandMethod();
        method.FinalValue!.SetIndicatedValue(3_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_cost_Land_method_with_no_unit_is_left_alone_not_treated_as_a_lump()
    {
        var method = BuildForgottenUnitLandMethod();
        method.FinalValue!.SetIndicatedValue(3_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_market_Land_method_with_no_unit_clears_as_before()
    {
        var method = BuildForgottenUnitLandMethod();
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_Land_method_on_a_lumpsum_unit_with_no_value_yet_writes_nothing()
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        method.SetRole("Land");   // no MethodValue, no IndicatedValue, no unit yet

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Role_Land_with_a_building_never_copies_the_total_into_land_value()
    {
        var method = BuildLumpMethod("Land");
        method.FinalValue!.SetLandAreaValues(510m, 3_000_000m);
        method.FinalValue.SetBuildingValue(2_000_000m);
        method.FinalValue.SetIndicatedValue(5_000_000m);   // land + building

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(3_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_method_clears_land_figures_left_by_an_earlier_save_once_it_is_LandAndBuilding()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Clearing_a_market_method_leaves_the_appraisers_own_flag_alone()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        // IncludeLandArea is the appraiser's answer to "does this method price land at all".
        // Clearing our derived figures must not overwrite it — ExcludeLandArea would have.
        Assert.True(method.FinalValue!.IncludeLandArea);
    }

    [Fact]
    public void A_market_LandAndBuilding_method_can_have_its_land_area_inclusion_turned_back_on()
    {
        // The flag drives whether the book prints this group's พื้นที่ / ราคาต่อหน่วย columns, so it
        // has to follow what the appraiser sets even on a method that records no land figures.
        // SetLandAreaValues is the only other writer that turns it on, and this method never
        // reaches it — so without this, unticking and re-ticking left the flag false forever.
        var method = BuildRatePricedMethod("LandAndBuilding");
        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: false, buildingWasPresentBeforeThisSave: false);
        Assert.False(method.FinalValue!.IncludeLandArea);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: true, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void An_explicit_exclusion_clears_a_Land_market_method()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_leaves_a_market_LandAndBuilding_method_alone()
    {
        // IndicatedValue spans land AND building there, so it must never reach LandValue.
        var method = BuildRatePricedMethod("LandAndBuilding");
        method.FinalValue!.SetIndicatedValue(300_000_000m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_leaves_a_method_with_no_Role_alone()
    {
        // Legacy cost rows and Income/Residual both have a null Role; the sync keys on Role = Land only.
        var method = BuildRatePricedMethod(role: null);
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(300_000_000m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_lumpsum_cost_Land_method_records_its_whole_value_as_land()
    {
        // Leasehold and ProfitRent sit under the cost approach with Role=Land and price a whole unit.
        // Role=Land means the whole value IS the land value, whatever the unit.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "Leasehold");
        method.SetValue(6_796_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 6_796_000m));
        method.SetRole("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(6_796_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_lumpsum_cost_method_that_is_not_Land_is_left_alone()
    {
        var method = BuildLumpMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_sets_land_value_to_the_indicated_value_on_a_lumpsum_Land_method()
    {
        // Role=Land: the whole value is land on a lump unit too. ApplyLandAreaValue writes a fresh net
        // area every save, so the area read here is not a leftover from an earlier unit.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(6_796_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 6_796_000m));
        method.SetRole("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(6_796_000m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(6_796_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Switching_back_to_system_calculation_drops_the_typed_over_total()
    {
        // "Compute this from the comparables" has to include forgetting the manual figure. Left
        // behind, SyncMethodValueWithIndicatedValue pushes it back over MethodValue on the next save
        // and the method never moves with its comparables again.
        var method = BuildRatePricedMethod("Land");
        method.RecordCalcMode(false);
        method.FinalValue!.SetIndicatedValue(5_000_000m);

        method.SetCalcMode(true);

        Assert.Null(method.FinalValue.IndicatedValue);
    }

    [Fact]
    public void Cost_Land_method_priced_per_sqm_takes_the_lump_path_and_ignores_a_false()
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(21_375m, 21_375m, "PerSqm");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 21_375m));
        method.SetRole("Land");
        method.FinalValue!.SetIndicatedValue(3_000_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(510m, method.FinalValue.LandArea);
        Assert.Equal(3_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_non_Land_row_priced_per_sqm_clears_the_old_per_sqwa_figure_in_cost_and_market_alike()
    {
        var cost = BuildRatePricedMethod("LandAndBuilding");
        cost.SetValue(21_375m, 21_375m, "PerSqm");
        cost.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        var market = BuildRatePricedMethod("LandAndBuilding");
        market.SetValue(21_375m, 21_375m, "PerSqm");
        market.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        cost.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        market.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(cost.FinalValue.LandValue);
        Assert.True(cost.FinalValue.IncludeLandArea);   // flag untouched
        Assert.Null(market.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_LandAndBuilding_priced_per_sqm_stores_an_explicit_land_value()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");
        method.SetValue(21_375m, 21_375m, "PerSqm");

        method.ApplyLandAreaValue(510m, explicitLandValue: 12_000_000m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);   // not Land: skipped

        Assert.Equal(510m, method.FinalValue!.LandArea);
        Assert.Equal(12_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_cost_PerUnit_lump_that_is_not_Land_ignores_an_explicit_land_value()
    {
        var method = BuildLumpMethod("LandAndBuilding");

        method.ApplyLandAreaValue(510m, explicitLandValue: 12_000_000m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void On_PerSqWa_a_positive_explicit_land_value_wins_over_area_times_rate()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: 12_000_000m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(12_000_000m, method.FinalValue.LandValue);   // not 286,266,680
    }

    [Fact]
    public void On_PerSqWa_with_no_explicit_value_and_no_rate_a_stale_figure_is_cleared_and_yes_keeps_the_flag()
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(0m, null, "PerSqWa");   // a land-rate unit, but no rate anywhere
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        method.SetRole("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.ExcludeLandArea();   // flag off, as after an earlier exclusion
        method.FinalValue.SetFinalValueUnitType("PerSqWa");

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: true, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
        Assert.True(method.FinalValue.IncludeLandArea);
    }

    [Fact]
    public void A_Cost_PerSqm_non_Land_clear_lets_an_explicit_yes_turn_the_flag_back_on()
    {
        var method = BuildRatePricedMethod("LandAndBuilding");
        method.SetValue(21_375m, 21_375m, "PerSqm");
        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        Assert.False(method.FinalValue!.IncludeLandArea);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: true, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void An_explicit_land_value_of_zero_is_never_stored_as_land()
    {
        var perSqm = BuildRatePricedMethod("LandAndBuilding");
        perSqm.SetValue(21_375m, 21_375m, "PerSqm");
        perSqm.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        var perSqWa = BuildRatePricedMethod("Land");

        perSqm.ApplyLandAreaValue(510m, explicitLandValue: 0m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        perSqWa.ApplyLandAreaValue(LandArea, explicitLandValue: 0m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(perSqm.FinalValue.LandValue);                       // PerSqm: cleared, not stored as 0
        Assert.Equal(ExpectedLandValue, perSqWa.FinalValue!.LandValue); // PerSqWa: area x rate instead
    }

    [Fact]
    public void A_cost_row_with_an_unknown_unit_that_was_excluded_stays_untouched_despite_an_explicit_value()
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));   // no unit anywhere
        method.FinalValue!.ExcludeLandArea();

        method.ApplyLandAreaValue(510m, explicitLandValue: 12_000_000m, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_with_a_non_positive_indicated_value_clears_even_with_a_method_value()
    {
        // A typed figure of 0 or less means the method is valued at nothing; no fallback to the method
        // value. Matches the sync's clear.
        var method = BuildLumpMethod("Land");   // MethodValue 6,796,000
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(0m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_with_neither_a_positive_indicated_nor_method_value_clears()
    {
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        method.SetRole("Land");
        method.FinalValue!.SetFinalValueUnitType("PerUnit");
        method.FinalValue.SetIndicatedValue(-5m);
        method.FinalValue.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_clears_the_land_figures_for_a_non_positive_indicated_value_and_leaves_the_flag()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(0m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
        Assert.True(method.FinalValue.IncludeLandArea);
    }

    [Fact]
    public void Sync_with_no_indicated_value_keeps_what_the_row_had()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void A_group_with_no_net_titled_land_lets_an_explicit_yes_turn_the_flag_back_on()
    {
        var method = BuildRatePricedMethod("Land");
        method.ApplyLandAreaValue(0m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        Assert.False(method.FinalValue!.IncludeLandArea);

        method.ApplyLandAreaValue(0m, explicitLandValue: null, includeLandArea: true, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
    }

    [Fact]
    public void A_group_with_no_net_titled_land_and_false_is_excluded()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(0m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_group_with_no_net_titled_land_clears_a_stale_area_and_the_sync_then_writes_nothing()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);   // stale, from a larger parcel
        method.FinalValue.SetIndicatedValue(300_000_000m);

        method.ApplyLandAreaValue(0m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.True(method.FinalValue.IncludeLandArea);   // flag untouched
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_with_no_figure_clears_earlier_per_area_figures()
    {
        // No MethodValue, no IndicatedValue; the unit is known only from the row's stamp.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        method.SetRole("Land");
        method.FinalValue!.SetFinalValueUnitType("PerUnit");
        method.FinalValue.SetLandAreaValues(LandArea, ExpectedLandValue);   // left by a per-area save

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Lump_Land_method_with_a_zero_method_value_counts_as_no_figure()
    {
        var method = BuildLumpMethod("Land");
        method.SetValue(0m, null, "PerUnit");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: null, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_reference_analysis_keeps_the_land_area_override_it_was_created_with()
    {
        // CreateReferenceFromMethod builds a "Market" approach and calls SetLandAreaValues to carry
        // the DCF non-HBU land-area override. A reference is not anchored to a property group, so the
        // titled area is null — and the approach check has to sit behind that guard, or the
        // reference's first save wipes the very figure it exists to hold.
        var method = BuildRatePricedMethod(role: null);
        method.FinalValue!.SetLandAreaValues(250m, 30_000_000m);

        method.ApplyLandAreaValue(null, explicitLandValue: null, includeLandArea: null, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Equal(250m, method.FinalValue.LandArea);
        Assert.Equal(30_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Switching_to_manual_keeps_the_typed_over_total()
    {
        // The opposite flip is the appraiser committing their own figure, and the board echoes the
        // current value along with it. Clearing here dropped that figure, and UpdateMethod's "the
        // value actually moved" gate then refused to re-stamp an unchanged number — so the method
        // stopped being pinned at the very moment it was meant to start.
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetIndicatedValue(12_000_000m);

        method.SetCalcMode(false);

        Assert.Equal(12_000_000m, method.FinalValue.IndicatedValue);
    }

    [Fact]
    public void An_explicit_exclusion_still_wins_for_a_cost_method()
    {
        var method = BuildRatePricedMethod("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue!.IncludeLandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_leaves_a_role_Land_method_that_carries_a_building_alone()
    {
        // SetFinalValue and UpdateFinalValue write a building value without re-tagging the role, so
        // a Role=Land row can carry a building. IndicatedValue then spans land AND building, and
        // copying it into LandValue would hand the building to every reader that treats this column
        // as land — each of which adds the building again from its own source.
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(300_000_000m); // land + building
        method.FinalValue.SetBuildingValue(13_733_320m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: true);

        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_still_runs_without_a_fresh_indicated_value_when_no_building_was_ever_in_it()
    {
        // The common case: a land-only method saved through a screen that sends no IndicatedValue.
        // No building has ever been in this row, so the stored IndicatedValue can only describe
        // land. Refusing here would let ApplyLandAreaValue's raw area × rate replace the figure the
        // appraiser committed — up to 999 baht apart on the manual-cost path.
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(286_267_000m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(286_267_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_ignores_an_indicated_value_written_while_a_building_was_included()
    {
        // The save that unticks "include building" clears the flag, so keying on the flag's new
        // value alone would let the previous save's land+building total into LandValue. A value
        // arriving on the same request is no help either — the board echoes the stored figure back,
        // so an echoed combined total is indistinguishable from a recomputed land-only one.
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(300_000_000m); // written when the building was included
        method.FinalValue.ClearBuildingValue();

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: true);

        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_copies_the_indicated_value_when_there_is_no_building()
    {
        var method = BuildRatePricedMethod("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);
        method.FinalValue.SetIndicatedValue(286_267_000m); // the appraiser's rounded figure

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(286_267_000m, method.FinalValue.LandValue);
    }

    private static PricingAnalysisMethod BuildLumpMethodWithBuilding(string? role)
    {
        var method = BuildLumpMethod(role);
        method.FinalValue!.SetBuildingValue(2_000_000m);
        return method;
    }

    [Fact]
    public void Cost_LandAndBuilding_lump_with_a_building_stores_the_typed_land_value_even_when_false_is_sent()
    {
        // "Include building" ticked on a lumpsum comparable: the screen derives includeLandArea = false
        // from the unit (the appraiser never chose it), but the typed land value must survive.
        var method = BuildLumpMethodWithBuilding("LandAndBuilding");

        method.ApplyLandAreaValue(510m, explicitLandValue: 3_000_000m, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);   // Role != Land: skipped

        Assert.Equal(510m, method.FinalValue!.LandArea);
        Assert.Equal(3_000_000m, method.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_LandAndBuilding_lump_with_a_building_and_no_typed_value_clears_land_figures()
    {
        var method = BuildLumpMethodWithBuilding("LandAndBuilding");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: null, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Cost_LandAndBuilding_lump_without_a_building_still_honours_false()
    {
        var method = BuildLumpMethod("LandAndBuilding");

        method.ApplyLandAreaValue(510m, explicitLandValue: 3_000_000m, includeLandArea: false, isCostApproach: true, buildingWasPresentBeforeThisSave: false);

        Assert.False(method.FinalValue!.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_LandAndBuilding_lump_with_a_building_still_clears_despite_a_typed_land_value()
    {
        var method = BuildLumpMethodWithBuilding("LandAndBuilding");
        method.FinalValue!.SetLandAreaValues(510m, 750_000m);

        method.ApplyLandAreaValue(510m, explicitLandValue: 3_000_000m, includeLandArea: false, isCostApproach: false, buildingWasPresentBeforeThisSave: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }
}
