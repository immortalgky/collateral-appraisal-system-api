using Appraisal.Application.Services;

namespace Appraisal.Tests.Application;

/// <summary>
/// The three milestone figures the Decision Summary card and the construction summary book both
/// print. They used to be the plain sum of land + finished buildings + inspected buildings, which
/// let the card disagree with the price the pricing screen settled on; they now report the appraised
/// value of the inspected groups and cap the part-built figures at it.
/// </summary>
public class ConstructionValueBreakdownTests
{
    private static ConstructionValueBreakdown Build(
        decimal appraised, decimal land = 130_000_000m, decimal completed = 1_000_000m,
        decimal inspectedTotal = 4_000_000m, decimal inspectedCurrent = 1_993_200m,
        decimal inspectedPrevious = 1_000_000m, decimal currentPct = 49.83m)
        => new(
            LandValue: land,
            CompletedBuildingValue: completed,
            InspectedTotalValue: inspectedTotal,
            InspectedPreviousValue: inspectedPrevious,
            InspectedCurrentValue: inspectedCurrent,
            UnweightedPreviousPercent: 25m,
            UnweightedCurrentPercent: currentPct,
            WeightedPreviousPercent: 25m,
            WeightedCurrentPercent: currentPct,
            HasOwnValueBase: true,
            AppraisedValue: appraised);

    [Fact]
    public void Complete_reports_the_appraised_value_not_the_sum_of_its_parts()
    {
        // The Cost approach priced the group at 136m while the depreciation tables and the
        // inspection add up to 135m. The book prints the price of record, so the card must too.
        var b = Build(appraised: 136_000_000m);

        Assert.Equal(136_000_000m, b.CompleteValue);
        Assert.Equal(135_000_000m, b.LandValue + b.CompletedBuildingValue + b.InspectedTotalValue);
    }

    [Fact]
    public void Complete_falls_back_to_the_components_when_the_group_carries_no_price()
    {
        var b = Build(appraised: 0m);

        Assert.Equal(135_000_000m, b.CompleteValue);
    }

    [Fact]
    public void Current_is_the_progress_figure_while_the_work_is_unfinished()
    {
        var b = Build(appraised: 136_000_000m);

        Assert.True(b.IsUnderConstruction);
        Assert.Equal(132_993_200m, b.CurrentValue);
        Assert.Equal(132_000_000m, b.PreviousValue);
    }

    [Fact]
    public void Current_equals_complete_once_the_work_is_finished()
    {
        // Nothing is left to build, so reporting less would say a finished property is worth less
        // than its own appraisal.
        var b = Build(appraised: 136_000_000m, inspectedCurrent: 4_000_000m, currentPct: 100m);

        Assert.False(b.IsUnderConstruction);
        Assert.Equal(136_000_000m, b.CurrentValue);
    }

    [Fact]
    public void Ninety_nine_point_nine_nine_percent_is_still_unfinished()
    {
        // IsUnderConstruction reads the unrounded percentage: 99.995 displays as 100.00, and keying
        // off the displayed figure would pay out a still-unfinished building at its completed price.
        var b = Build(appraised: 136_000_000m, currentPct: 99.995m);

        Assert.Equal(100.00m, b.ConstructionProgressPercent);
        Assert.True(b.IsUnderConstruction);
        Assert.Equal(132_993_200m, b.CurrentValue);
    }

    [Fact]
    public void A_part_built_figure_never_prints_above_the_finished_one()
    {
        // The two come from different bases — the appraiser priced the group below what the
        // inspection's totals imply — so without the cap the unfinished row read higher than the
        // completed row above it.
        var b = Build(appraised: 131_000_000m);

        Assert.Equal(131_000_000m, b.CompleteValue);
        Assert.Equal(131_000_000m, b.CurrentValue);
        Assert.Equal(131_000_000m, b.PreviousValue);
    }

    [Fact]
    public void A_round_that_was_already_finished_reports_no_further_progress()
    {
        // Re-inspecting a building that was complete last round: capping the previous figure without
        // lifting it left Previous on the components and Current on the appraised value, so the card
        // printed 0.00% progress beside a 1m increase.
        var b = Build(appraised: 136_000_000m,
                      inspectedPrevious: 4_000_000m, inspectedCurrent: 4_000_000m, currentPct: 100m);
        var finished = b with { WeightedPreviousPercent = 100m, UnweightedPreviousPercent = 100m };

        Assert.Equal(136_000_000m, finished.CompleteValue);
        Assert.Equal(136_000_000m, finished.CurrentValue);
        Assert.Equal(136_000_000m, finished.PreviousValue);
        Assert.Equal(0m, finished.CurrentValue - finished.PreviousValue);
    }

    [Fact]
    public void A_condo_unit_reports_its_appraised_value_at_every_milestone()
    {
        // No depreciation table to total, so the service substitutes the appraised value into the
        // inspected figures and drops land and finished buildings to avoid counting them twice.
        var b = new ConstructionValueBreakdown(
            LandValue: 0m,
            CompletedBuildingValue: 0m,
            InspectedTotalValue: 3_000_000m,
            InspectedPreviousValue: 3_000_000m,
            InspectedCurrentValue: 3_000_000m,
            UnweightedPreviousPercent: 30m,
            UnweightedCurrentPercent: 60m,
            WeightedPreviousPercent: 0m,
            WeightedCurrentPercent: 0m,
            HasOwnValueBase: false,
            AppraisedValue: 3_000_000m);

        Assert.Equal(3_000_000m, b.CompleteValue);
        Assert.Equal(3_000_000m, b.CurrentValue);
        Assert.Equal(60m, b.ConstructionProgressPercent);   // progress still reported, money unmoved
    }
}
