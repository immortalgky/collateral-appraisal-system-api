using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Guards the rule that only the COST approach records a land figure of its own — it is the one
/// approach that breaks a collateral into named components.
/// <para>
/// Market, income and residual price the collateral as a single figure. Their comparables are
/// routinely quoted per square wa, which used to be read as "this method prices land by area" and
/// multiplied out, writing the WHOLE property's value (buildings included) into a column called
/// LandValue that the book, LOS, MIS and the AS400 regulatory file all read as land.
/// </para>
/// <para>
/// The gate takes the approach type as an argument rather than inferring it from <c>Role</c>. Role
/// is null on non-cost methods, but it is ALSO null on cost rows that predate it — so inferring
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

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true);

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

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true);

        Assert.Equal(LandArea, method.FinalValue!.LandArea);
        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_method_records_no_land_figures_despite_a_per_area_rate()
    {
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void An_explicitly_supplied_land_value_does_not_rescue_a_market_method()
    {
        // There is no separable land figure to record on a lump-priced collateral, so a number the
        // client sends is refused rather than stored — otherwise the column would still be wrong,
        // just wrong with a different provenance.
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: 123_000_000m, includeLandArea: null, isCostApproach: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Market_method_clears_land_figures_left_by_an_earlier_save()
    {
        var method = BuildRatePricedMethod(role: null);
        // A row written under the old rule, before the gate existed.
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false);

        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Clearing_a_market_method_leaves_the_appraisers_own_flag_alone()
    {
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false);

        // IncludeLandArea is the appraiser's answer to "does this method price land at all".
        // Clearing our derived figures must not overwrite it — ExcludeLandArea would have.
        Assert.True(method.FinalValue!.IncludeLandArea);
    }

    [Fact]
    public void A_market_method_can_have_its_land_area_inclusion_turned_back_on()
    {
        // The flag drives whether the book prints this group's พื้นที่ / ราคาต่อหน่วย columns, so it
        // has to follow what the appraiser sets even on a method that records no land figures.
        // SetLandAreaValues is the only other writer that turns it on, and a non-cost method never
        // reaches it — so without this, unticking and re-ticking left the flag false forever.
        var method = BuildRatePricedMethod(role: null);
        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: false);
        Assert.False(method.FinalValue!.IncludeLandArea);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: true, isCostApproach: false);

        Assert.True(method.FinalValue.IncludeLandArea);
        Assert.Null(method.FinalValue.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_market_method_on_a_land_only_collateral_still_records_nothing()
    {
        // The deliberate choice, made 2026-09-23 against the alternative of being clever about it.
        // On a plot with no structures the market figure IS entirely land, so writing it would have
        // been correct — 3 of the 7 rows carrying a market LandValue at the time were exactly that.
        // It is still not written, because "market prices the collateral as one lump" is easier to
        // reason about than a rule whose answer depends on what else happens to be in the group.
        // ApplyLandAreaValue never looks at the group's composition; this test exists so nobody
        // "fixes" that by reintroducing the exception.
        var method = BuildRatePricedMethod(role: null);

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: false);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void A_lumpsum_cost_method_records_nothing_either()
    {
        // Leasehold and ProfitRent sit under the cost approach with Role=Land but price a whole
        // unit, so they carry no land rate to multiply — the unit check turns them away before the
        // rate is read. Confirmed against 69000170 in the 2026-09-24 run.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "Leasehold");
        method.SetValue(6_796_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 6_796_000m));
        method.SetRole("Land");

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: null, isCostApproach: true);

        Assert.Null(method.FinalValue!.LandArea);
        Assert.Null(method.FinalValue.LandValue);
    }

    [Fact]
    public void Sync_leaves_a_lumpsum_method_alone_even_when_an_old_land_area_lingers()
    {
        // A whole-unit lumpsum carries no rate, so ApplyLandAreaValue turns it away — and the sync has
        // to turn it away for the same reason. The LandArea on the row is whatever an earlier save
        // under a rate unit left behind, and writing the indicated value against it both resurrects a
        // figure that should be gone and forces IncludeLandArea back to true.
        var method = PricingAnalysisMethod.Create(Guid.NewGuid(), "SaleGrid");
        method.SetValue(6_796_000m, null, "PerUnit");
        method.SetFinalValue(PricingFinalValue.Create(method.Id, 6_796_000m));
        method.SetRole("Land");
        method.FinalValue!.SetLandAreaValues(LandArea, ExpectedLandValue); // stale, from a rate-priced save
        method.FinalValue.SetIndicatedValue(6_796_000m);

        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave: false);

        Assert.Equal(ExpectedLandValue, method.FinalValue.LandValue);
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
    public void A_reference_analysis_keeps_the_land_area_override_it_was_created_with()
    {
        // CreateReferenceFromMethod builds a "Market" approach and calls SetLandAreaValues to carry
        // the DCF non-HBU land-area override. A reference is not anchored to a property group, so the
        // titled area is always 0 — and the approach check has to sit behind that guard, or the
        // reference's first save wipes the very figure it exists to hold.
        var method = BuildRatePricedMethod(role: null);
        method.FinalValue!.SetLandAreaValues(250m, 30_000_000m);

        method.ApplyLandAreaValue(0m, explicitLandValue: null, includeLandArea: null, isCostApproach: false);

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

        method.ApplyLandAreaValue(LandArea, explicitLandValue: null, includeLandArea: false, isCostApproach: true);

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
}
