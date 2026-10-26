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
}
