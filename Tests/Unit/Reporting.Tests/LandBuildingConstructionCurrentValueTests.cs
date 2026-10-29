using FluentAssertions;
using Reporting.Application.Models;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>
/// While a building is under construction, a property's printed current-condition lines must add up to the
/// inspection's stored current value (which an inspector may have overridden), not to value x percent.
/// </summary>
public class LandBuildingConstructionCurrentValueTests
{
    private static readonly Guid Property = Guid.NewGuid();

    private static SummaryItemRow Line(decimal value) => new() { Value = value, CurrentValue = value * 0.3m };

    private static (Guid, SummaryItemRow, bool) Building(SummaryItemRow row) => (Property, row, true);

    private static (Guid, SummaryItemRow, bool) Development(SummaryItemRow row) => (Property, row, false);

    private static void Reconcile(decimal? pct, decimal? stored, params (Guid, SummaryItemRow, bool)[] lines) =>
        AppraisalSummaryLandBuildingDataProvider.ReconcileToStored(
            lines, _ => pct,
            stored is { } s ? lines.Select(l => l.Item1).Distinct().ToDictionary(id => id, _ => s) : []);

    [Fact]
    public void Overridden_property_prints_lines_that_sum_to_the_stored_value()
    {
        // 8,000,000 at 30% scales to 2,400,000; the stored (overridden) figure is 2,000,000.
        var building = Line(8_000_000m);

        Reconcile(30m, 2_000_000m, Building(building));

        building.CurrentValue.Should().Be(2_000_000m);
    }

    [Fact]
    public void Not_overridden_keeps_the_plain_scaled_figures()
    {
        var building = Line(7_000_000m);
        var development = Line(1_000_000m);

        Reconcile(30m, 2_400_000m, Building(building), Development(development));

        building.CurrentValue.Should().Be(2_100_000m);
        development.CurrentValue.Should().Be(300_000m);
    }

    [Fact]
    public void Several_buildings_share_the_stored_value_in_proportion_and_the_largest_line_absorbs_rounding()
    {
        var a = Line(3_000_000m);
        var b = Line(3_000_000m);
        var c = Line(2_000_000m);

        Reconcile(30m, 2_000_001m, Building(a), Building(b), Building(c));

        b.CurrentValue.Should().Be(750_000.38m);
        c.CurrentValue.Should().Be(500_000.25m);
        a.CurrentValue.Should().Be(750_000.37m);   // first of the largest lines: stored less the others
        (a.CurrentValue + b.CurrentValue + c.CurrentValue).Should().Be(2_000_001m);
    }

    [Fact]
    public void A_development_heavy_property_cannot_go_negative()
    {
        // Development lines alone would claim 900,000 of a 600,000 stored value; the proportional split cannot.
        var building = Line(1_000_000m);
        var development = Line(2_000_000m);

        Reconcile(30m, 600_000m, Building(building), Development(development));

        building.CurrentValue.Should().BeGreaterThanOrEqualTo(0m);
        (building.CurrentValue + development.CurrentValue).Should().Be(600_000m);
    }

    [Fact]
    public void Each_property_is_reconciled_to_its_own_stored_value()
    {
        var other = Guid.NewGuid();
        var first = Line(8_000_000m);
        var second = Line(4_000_000m);

        AppraisalSummaryLandBuildingDataProvider.ReconcileToStored(
            [(Property, first, true), (other, second, true)], _ => 30m,
            new Dictionary<Guid, decimal> { [Property] = 2_000_000m, [other] = 1_200_000m });

        first.CurrentValue.Should().Be(2_000_000m);
        second.CurrentValue.Should().Be(1_200_000m);
    }

    [Fact]
    public void Edge_rules_leave_the_lines_untouched()
    {
        foreach (var pct in new decimal?[] { null, 0m, 100m })
        {
            var line = Line(8_000_000m);
            var before = line.CurrentValue;

            Reconcile(pct, 2_000_000m, Building(line));

            line.CurrentValue.Should().Be(before);
        }

        var noStored = Line(8_000_000m);
        Reconcile(30m, null, Building(noStored));
        noStored.CurrentValue.Should().Be(2_400_000m);
    }

    [Fact]
    public void The_rounding_residue_goes_to_the_largest_line_not_to_a_zero_value_one()
    {
        var building = Line(3_000_000m);
        var zero = Line(0m);

        Reconcile(30m, 899_999.99m, Building(building), Development(zero));

        zero.CurrentValue.Should().Be(0m);
        building.CurrentValue.Should().Be(899_999.99m);
    }

    [Fact]
    public void A_property_without_a_valued_building_line_is_left_pct_scaled()
    {
        var development = Line(1_000_000m);
        var building = new SummaryItemRow { Value = null, CurrentValue = null };

        Reconcile(30m, 2_000_000m, Building(building), Development(development));

        development.CurrentValue.Should().Be(300_000m);
    }

    [Fact]
    public void A_property_with_a_negative_line_is_left_pct_scaled()
    {
        var building = Line(8_000_000m);
        var negative = new SummaryItemRow { Value = -100m, CurrentValue = -30m };

        Reconcile(30m, 2_000_000m, Building(building), Development(negative));

        building.CurrentValue.Should().Be(2_400_000m);
    }

    [Fact]
    public void Shortfall_is_floored_per_property_so_an_overridden_one_cannot_cancel_another()
    {
        var other = Guid.NewGuid();
        var overridden = Line(8_000_000m);   // current 9,000,000: override above its 100% sum
        overridden.CurrentValue = 9_000_000m;
        var behind = Line(4_000_000m);       // current 1,200,000: 2,800,000 still to build

        var shortfall = AppraisalSummaryLandBuildingDataProvider.ShortfallPerProperty(
            [(Property, overridden), (other, behind)]);

        shortfall.Should().Be(2_800_000m);
    }

    [Fact]
    public void A_development_only_property_stays_pct_scaled()
    {
        var fence = Line(1_000_000m);
        var pavement = Line(500_000m);

        Reconcile(30m, 400_000m, Development(fence), Development(pavement));

        fence.CurrentValue.Should().Be(300_000m);
        pavement.CurrentValue.Should().Be(150_000m);
    }

    // ── Development-only buildings: one line, on the Building Cost Value ──────────────────────────

    private static AppraisalSummaryLandBuildingDataProvider.GroupBuildingRow Bld(
        Guid id, decimal? area = 80m, int? age = 0) => new()
        {
            BuildingId = id, AppraisalPropertyId = Property, BuildingTypeDisplay = "รั้วตาข่ายเหล็ก",
            TotalBuildingArea = area, BuildingAge = age
        };

    private static AppraisalSummaryLandBuildingDataProvider.GroupDepreciationRow Dep(
        Guid buildingId, bool isBuilding, decimal? price, decimal? keyed = null, decimal area = 77.77m, short year = 1) => new()
        {
            BuildingAppraisalDetailId = buildingId, AppraisalPropertyId = Property, IsBuilding = isBuilding,
            AreaDescription = "ลานคอนกรีต", Area = area, Year = year, PriceAfterDepreciation = price,
            BuildingCostValue = keyed
        };

    private static List<(Guid AppraisalPropertyId, bool IsMoved, SummaryItemRow Row)> Rows(
        decimal? pct, AppraisalSummaryLandBuildingDataProvider.GroupBuildingRow[] buildings,
        params AppraisalSummaryLandBuildingDataProvider.GroupDepreciationRow[] deps) =>
        AppraisalSummaryLandBuildingDataProvider.BuildDevelopmentRows(
            buildings, deps, AppraisalSummaryLandBuildingDataProvider.DevelopmentOnlyBuildingIds(deps), _ => pct);

    [Fact]
    public void Development_only_building_prints_the_keyed_value_and_reconciles_to_the_stored_current_value()
    {
        var id = Guid.NewGuid();

        var rows = Rows(70m, [Bld(id)], Dep(id, false, 7_699_230m, keyed: 7_699_000m));
        var line = rows.Should().ContainSingle().Subject;
        line.Row.CurrentValue.Should().Be(5_389_300m);   // pct-scaled before reconcile

        // The inspector's stored override differs from value x pct, so only a real reconcile can print it.
        AppraisalSummaryLandBuildingDataProvider.ReconcileToStored(
            [.. rows.Select(x => (x.AppraisalPropertyId, x.Row, OnInspectionBase: x.IsMoved))], _ => 70m,
            new Dictionary<Guid, decimal> { [Property] = 5_500_000m });

        line.IsMoved.Should().BeTrue();
        line.Row.Value.Should().Be(7_699_000m);
        line.Row.CurrentValue.Should().Be(5_500_000m);
        // Area and age come from the property level (80 m², age 0 skipped), never the row's 77.77 / year 1.
        line.Row.Description.Should().Be("รั้วตาข่ายเหล็ก พื้นที่ 80 ตารางเมตร (แล้วเสร็จ 70%)");
    }

    [Fact]
    public void Development_only_building_without_a_keyed_value_rounds_to_1000()
    {
        var id = Guid.NewGuid();

        var rows = Rows(null, [Bld(id, age: 3)], Dep(id, false, 7_699_230m));

        rows.Single().Row.Value.Should().Be(7_699_000m);
        rows.Single().Row.CurrentValue.Should().Be(7_699_000m);
        rows.Single().Row.Description.Should().Be("รั้วตาข่ายเหล็ก พื้นที่ 80 ตารางเมตร อายุ 3 ปี");
    }

    [Fact]
    public void Development_only_building_with_two_rows_is_still_one_line_and_skips_unstated_area()
    {
        var id = Guid.NewGuid();

        var rows = Rows(null, [Bld(id, area: null)], Dep(id, false, 400_400m), Dep(id, false, 300_200m));

        var line = rows.Should().ContainSingle().Subject;
        line.Row.Value.Should().Be(701_000m);   // 700,600 rounded to 1,000
        line.Row.Description.Should().Be("รั้วตาข่ายเหล็ก");
    }

    [Fact]
    public void Development_rows_under_a_normal_building_stay_one_line_per_row_on_raw_depreciation()
    {
        var id = Guid.NewGuid();

        var rows = Rows(null, [Bld(id)],
            Dep(id, true, 5_000_000m, keyed: 5_800_000m),
            Dep(id, false, 400_400m),
            Dep(id, false, 300_200m, area: 10m, year: 2));

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(x => !x.IsMoved);
        rows.Select(x => x.Row.Value).Should().Equal(400_400m, 300_200m);
        rows[0].Row.Description.Should().Be("ลานคอนกรีต พื้นที่ 77.77 ตารางเมตร อายุ 1 ปี");
        rows[1].Row.Description.Should().Be("ลานคอนกรีต พื้นที่ 10 ตารางเมตร อายุ 2 ปี");
    }

    [Fact]
    public void Unkeyed_and_unpriced_development_only_building_prints_blank_values()
    {
        var id = Guid.NewGuid();

        var rows = Rows(70m, [Bld(id)], Dep(id, false, price: null));

        rows.Single().Row.Value.Should().BeNull();
        rows.Single().Row.CurrentValue.Should().BeNull();
    }
}
