using Appraisal.Domain.Appraisals;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Domain;

/// <summary>
/// The four Summary* figures are the inspection-level result in BOTH modes: keyed percentages with
/// server-derived money in summary mode, everything derived from the work rows in full-detail mode.
/// Every write path goes through the aggregate, so the figures are pinned here once.
/// </summary>
public class ConstructionInspectionSummaryTests
{
    private static readonly Guid Group = Guid.NewGuid();

    private static ConstructionInspection Summary(decimal total, decimal? previousPct, decimal? currentPct) =>
        ConstructionInspection.CreateSummary(
            Guid.NewGuid(), total, summaryDetail: null,
            summaryPreviousProgressPct: previousPct, summaryCurrentProgressPct: currentPct, remark: null);

    private static ConstructionInspection Detail(decimal total, params (decimal Proportion, decimal Previous, decimal Current)[] rows)
    {
        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), total, remark: null);
        var order = 1;
        foreach (var (proportion, previous, current) in rows)
            ci.AddWorkDetail(Group, $"item {order}", order++, proportion, previous, current);
        ci.ComputeAllValues();
        return ci;
    }

    // ── summary mode ────────────────────────────────────────────────────────────

    [Fact]
    public void Summary_mode_keeps_the_keyed_percentages_and_derives_the_money()
    {
        var ci = Summary(2_000_000m, 25m, 60m);

        Assert.Equal(25m, ci.SummaryPreviousProgressPct);
        Assert.Equal(60m, ci.SummaryCurrentProgressPct);
        Assert.Equal(500_000m, ci.SummaryPreviousValue);
        Assert.Equal(1_200_000m, ci.SummaryCurrentValue);
    }

    [Theory]
    [InlineData(1_001, 50, 501)]   // 500.5 -> half away from zero (banker's rounding would give 500)
    [InlineData(1_001, 10, 100)]   // 100.1
    [InlineData(3_333, 15, 500)]   // 499.95
    [InlineData(10, 4.9, 0)]       // 0.49
    [InlineData(10, 5, 1)]         // 0.5
    public void Summary_money_is_whole_baht_rounded_half_away_from_zero(decimal total, decimal pct, decimal expected)
    {
        Assert.Equal(expected, Summary(total, null, pct).SummaryCurrentValue);
    }

    [Fact]
    public void A_percentage_that_was_never_keyed_has_no_value_either()
    {
        var ci = Summary(1_000_000m, null, null);

        Assert.Null(ci.SummaryPreviousValue);
        Assert.Null(ci.SummaryCurrentValue);
    }

    [Fact]
    public void Updating_a_summary_recomputes_the_money_against_the_new_base()
    {
        var ci = Summary(1_000m, 10m, 20m);

        ci.UpdateSummary(totalValue: 2_000m, summaryDetail: null,
            summaryPreviousProgressPct: 10m, summaryCurrentProgressPct: 30m, remark: null);

        Assert.Equal(200m, ci.SummaryPreviousValue);
        Assert.Equal(600m, ci.SummaryCurrentValue);
    }

    // ── full-detail mode ────────────────────────────────────────────────────────

    [Fact]
    public void Full_detail_mode_sums_the_weighted_rows_for_progress_and_value()
    {
        var ci = Detail(1_000_000m,
            (40m, 50m, 100m),   // contributes 20 -> 40
            (60m, 0m, 50m));    // contributes  0 -> 30

        Assert.Equal(70m, ci.SummaryCurrentProgressPct);
        Assert.Equal(20m, ci.SummaryPreviousProgressPct);
        Assert.Equal(700_000m, ci.SummaryCurrentValue);
        Assert.Equal(200_000m, ci.SummaryPreviousValue);
    }

    [Fact]
    public void Full_detail_value_is_the_sum_of_the_already_rounded_rows()
    {
        // 1,001 x 33.3333% = 333.67 -> 334 per row; the chain rounds each row, then sums them.
        var ci = Detail(1_001m, (33.3333m, 0m, 100m), (33.3333m, 0m, 100m), (33.3334m, 0m, 100m));

        var rows = ci.WorkDetails.Sum(d => d.CurrentPropertyValue);
        Assert.Equal(rows, ci.SummaryCurrentValue);
        Assert.Equal(100m, ci.SummaryCurrentProgressPct);
    }

    [Fact]
    public void Full_detail_current_progress_sums_the_percentages_as_stored_four_decimals_each()
    {
        // 33.3333 x 33.3333% = 11.11108889 -> the column stores 11.1111 per row. The readers this replaced summed
        // the stored rows, so the stored sum has to be the sum of the rounded ones.
        var ci = Detail(1_000m, (33.3333m, 0m, 33.3333m), (33.3333m, 0m, 33.3333m));

        Assert.Equal(22.2222m, ci.SummaryCurrentProgressPct);
    }

    [Fact]
    public void Full_detail_previous_progress_is_summed_whole_and_rounded_once()
    {
        var ci = Detail(1_000m, (33.3333m, 33.3333m, 0m), (33.3333m, 33.3333m, 0m));

        Assert.Equal(22.2222m, ci.SummaryPreviousProgressPct); // 22.22217778 rounded to the column's 4 places
    }

    [Fact]
    public void Full_detail_without_work_rows_reports_zero_not_null()
    {
        var created = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 500m, remark: null);

        Assert.Equal(0m, created.SummaryCurrentProgressPct);
        Assert.Equal(0m, created.SummaryPreviousProgressPct);
        Assert.Equal(0m, created.SummaryCurrentValue);
        Assert.Equal(0m, created.SummaryPreviousValue);
    }

    [Fact]
    public void Clearing_the_work_rows_resets_the_figures()
    {
        var ci = Detail(1_000m, (100m, 0m, 50m));
        Assert.Equal(50m, ci.SummaryCurrentProgressPct);

        ci.ClearWorkDetails();

        Assert.Equal(0m, ci.SummaryCurrentProgressPct);
        Assert.Equal(0m, ci.SummaryCurrentValue);
    }

    [Fact]
    public void Recomputing_after_a_new_base_moves_the_values_but_not_the_percentages()
    {
        var ci = Detail(1_000m, (100m, 0m, 50m));

        ci.UpdateFullDetail(totalValue: 4_000m, remark: null);
        ci.ComputeAllValues();

        Assert.Equal(50m, ci.SummaryCurrentProgressPct);
        Assert.Equal(2_000m, ci.SummaryCurrentValue);
    }

    [Fact]
    public void Switching_mode_replaces_the_figures_with_the_new_modes_result()
    {
        var ci = Summary(1_000m, 10m, 40m);
        ci.UpdateFullDetail(totalValue: 1_000m, remark: null);
        ci.AddWorkDetail(Group, "item", 1, 100m, 20m, 70m);
        ci.ComputeAllValues();
        Assert.Equal(70m, ci.SummaryCurrentProgressPct);
        Assert.Equal(700m, ci.SummaryCurrentValue);

        ci.UpdateSummary(totalValue: 1_000m, summaryDetail: null,
            summaryPreviousProgressPct: 5m, summaryCurrentProgressPct: 15m, remark: null);

        Assert.Empty(ci.WorkDetails);
        Assert.Equal(15m, ci.SummaryCurrentProgressPct);
        Assert.Equal(150m, ci.SummaryCurrentValue);
    }

    // ── next round ──────────────────────────────────────────────────────────────

    [Fact]
    public void Next_round_of_a_summary_inspection_starts_with_current_equal_to_previous()
    {
        var next = ConstructionInspection.CopyForNextInspection(Summary(2_000m, 10m, 60m), Guid.NewGuid());

        Assert.Equal(60m, next.SummaryPreviousProgressPct);
        Assert.Equal(1_200m, next.SummaryPreviousValue);
        Assert.Equal(60m, next.SummaryCurrentProgressPct); // no progress yet this round, as the form shows
        Assert.Equal(1_200m, next.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_of_a_summary_inspection_that_never_keyed_a_current_stays_unkeyed()
    {
        var prior = ConstructionInspection.CreateSummary(
            Guid.NewGuid(), 2_000m, null, summaryPreviousProgressPct: 10m, summaryCurrentProgressPct: null, remark: null);

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Null(next.SummaryCurrentProgressPct);
        Assert.Null(next.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_of_a_full_detail_inspection_starts_with_each_rows_current_equal_to_previous()
    {
        var prior = Detail(1_000_000m, (40m, 50m, 100m), (60m, 0m, 50m));

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Equal([100m, 50m], next.WorkDetails.OrderBy(d => d.DisplayOrder).Select(d => d.CurrentProgressPct));
        Assert.Equal(70m, next.SummaryPreviousProgressPct);   // last round's current
        Assert.Equal(700_000m, next.SummaryPreviousValue);
        Assert.Equal(70m, next.SummaryCurrentProgressPct);    // unchanged until the inspector raises a row
        Assert.Equal(700_000m, next.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_can_pin_the_base_to_zero_instead_of_inheriting_a_legacy_one_in_summary_mode()
    {
        var next = ConstructionInspection.CopyForNextInspection(Summary(2_000m, 10m, 60m), Guid.NewGuid(), totalValueOverride: 0m);

        Assert.Equal(0m, next.TotalValue);
        Assert.Equal(60m, next.SummaryCurrentProgressPct);   // the figures still carry
        Assert.Equal(0m, next.SummaryPreviousValue);          // values recomputed on the new base
        Assert.Equal(0m, next.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_can_pin_the_base_to_zero_instead_of_inheriting_a_legacy_one_in_full_detail_mode()
    {
        var prior = Detail(1_000_000m, (40m, 50m, 100m), (60m, 0m, 50m));

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid(), totalValueOverride: 0m);

        Assert.Equal(0m, next.TotalValue);
        Assert.Equal(70m, next.SummaryCurrentProgressPct);
        Assert.All(next.WorkDetails, d => Assert.Equal(0m, d.CurrentPropertyValue));
        Assert.Equal(0m, next.SummaryCurrentValue);
    }

    // ── the 100% base ───────────────────────────────────────────────────────────

    private static BuildingAppraisalDetail NewBuilding() =>
        AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now).AddBuildingProperty().BuildingDetail!;

    [Fact]
    public void The_construction_base_is_the_keyed_cost_value_whole_baht()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: 700_000m);
        building.Update(buildingCostValue: 650_000.5m);
        building.ResolveDerivedValues();

        Assert.Equal(650_001m, building.ConstructionBaseValue);
    }

    [Fact]
    public void A_keyed_zero_is_kept_and_does_not_fall_back_to_the_schedule()
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: 700_000m);
        building.Update(buildingCostValue: 0m);
        building.ResolveDerivedValues();

        Assert.Equal(0m, building.ConstructionBaseValue);
    }

    [Theory]
    [InlineData(250_499.99, 250_000)]
    [InlineData(250_500, 251_000)]
    public void Without_a_keyed_value_the_base_is_the_schedule_rounded_to_a_thousand(decimal price, decimal expected)
    {
        var building = NewBuilding();
        building.AddDepreciationDetail("Gross", priceAfterDepreciation: price);

        Assert.Null(building.BuildingCostValue);          // not resolved yet: nothing keyed
        Assert.Equal(expected, building.ConstructionBaseValue);
    }

    [Fact]
    public void With_no_keyed_value_and_no_schedule_there_is_no_base()
    {
        Assert.Equal(0m, NewBuilding().ConstructionBaseValue);
    }
}
