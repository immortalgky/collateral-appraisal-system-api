using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// The inspector may override the inspection's current value (both modes). It is stored in
/// SummaryCurrentValue itself, bounded 0..TotalValue by the entity, and ignored when there is no value base.
/// </summary>
public class ConstructionInspectionCurrentValueOverrideTests
{
    private static readonly Guid Group = Guid.NewGuid();

    private static ConstructionInspection Summary(
        decimal total, decimal? previousPct, decimal? currentPct, decimal? currentValueOverride = null) =>
        ConstructionInspection.CreateSummary(
            Guid.NewGuid(), total, summaryDetail: null,
            summaryPreviousProgressPct: previousPct, summaryCurrentProgressPct: currentPct, remark: null,
            currentValueOverride: currentValueOverride);

    // Mirrors what the property appliers do: create/update, add the rows, then ComputeAllValues.
    private static void FillRows(ConstructionInspection ci, params (decimal Proportion, decimal Previous, decimal Current)[] rows)
    {
        ci.ClearWorkDetails();
        var order = 1;
        foreach (var (proportion, previous, current) in rows)
            ci.AddWorkDetail(Group, $"item {order}", order++, proportion, previous, current);
        ci.ComputeAllValues();
    }

    private static ConstructionInspection Detail(
        decimal total, decimal? currentValueOverride, params (decimal Proportion, decimal Previous, decimal Current)[] rows)
    {
        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), total, remark: null, currentValueOverride);
        FillRows(ci, rows);
        return ci;
    }

    // ── summary mode ────────────────────────────────────────────────────────────

    [Fact]
    public void Summary_override_is_stored_as_the_current_value_and_flagged()
    {
        // 1,000,000 x 96.67% = 966,700, but the figure worked out from 96.6666... is 966,667.
        var ci = Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m);

        Assert.Equal(966_667m, ci.SummaryCurrentValue);
        Assert.Equal(96.67m, ci.SummaryCurrentProgressPct);   // the percentage is untouched
        Assert.True(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Without_an_override_the_computed_value_is_stored_and_not_flagged()
    {
        var ci = Summary(1_000_000m, 90m, 96.67m);

        Assert.Equal(966_700m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void An_override_equal_to_the_computed_value_is_not_flagged()
    {
        Assert.False(Summary(1_000m, null, 50m, currentValueOverride: 500m).IsCurrentValueOverridden);
    }

    [Fact]
    public void The_override_is_rounded_to_whole_baht()
    {
        Assert.Equal(501m, Summary(1_000m, null, 50m, currentValueOverride: 500.5m).SummaryCurrentValue);
    }

    [Fact]
    public void Updating_without_an_override_drops_the_previous_one()
    {
        var ci = Summary(1_000m, null, 50m, currentValueOverride: 400m);

        ci.UpdateSummary(1_000m, null, null, 50m, null);

        Assert.Equal(500m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Updating_a_summary_with_an_override_stores_it()
    {
        var ci = Summary(1_000m, null, 50m);

        ci.UpdateSummary(1_000m, null, null, 50m, null, currentValueOverride: 480m);

        Assert.Equal(480m, ci.SummaryCurrentValue);
        Assert.True(ci.IsCurrentValueOverridden);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000.5)]
    [InlineData(1_000_000)]
    public void An_override_outside_zero_to_total_is_rejected(double value)
    {
        var amount = (decimal)value;

        Assert.Throws<DomainException>(() => Summary(1_000m, null, 50m, currentValueOverride: amount));

        var existing = Summary(1_000m, null, 50m);
        Assert.Throws<DomainException>(() => existing.UpdateSummary(1_000m, null, null, 50m, null, amount));
        Assert.Throws<DomainException>(() => existing.UpdateFullDetail(1_000m, null, amount));
        Assert.Throws<DomainException>(() => ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000m, null, amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000)]
    public void The_bounds_themselves_are_allowed(double value)
    {
        Assert.Equal((decimal)value, Summary(1_000m, null, 50m, currentValueOverride: (decimal)value).SummaryCurrentValue);
    }

    [Fact]
    public void With_no_value_base_the_override_is_ignored()
    {
        var ci = Summary(0m, null, 50m, currentValueOverride: 123_456m);

        Assert.Equal(0m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Moving_the_base_with_a_matching_value_is_not_an_override()
    {
        var ci = Summary(1_000m, null, 50m);

        ci.UpdateSummary(2_000m, null, null, 50m, null);

        Assert.Equal(1_000m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    // ── full-detail mode ────────────────────────────────────────────────────────

    [Fact]
    public void Full_detail_override_survives_the_rows_being_computed()
    {
        var ci = Detail(1_000_000m, 966_667m, (100m, 90m, 96.67m));

        Assert.Equal(966_667m, ci.SummaryCurrentValue);
        Assert.True(ci.IsCurrentValueOverridden);
        Assert.Equal(966_700m, ci.WorkDetails.Single().CurrentPropertyValue);   // work rows stay computed
    }

    [Fact]
    public void Full_detail_without_an_override_sums_the_rows_and_is_not_flagged()
    {
        var ci = Detail(1_000_000m, null, (100m, 90m, 96.67m));

        Assert.Equal(966_700m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Updating_full_detail_follows_the_appliers_sequence_and_keeps_the_override()
    {
        var ci = Summary(1_000m, null, 50m);

        ci.UpdateFullDetail(1_000m, null, currentValueOverride: 480m);
        FillRows(ci, (100m, 0m, 50m));

        Assert.Equal(480m, ci.SummaryCurrentValue);
        Assert.True(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Full_detail_with_no_rows_has_nothing_to_override()
    {
        var ci = ConstructionInspection.CreateFullDetail(Guid.NewGuid(), 1_000m, null, currentValueOverride: 10m);

        Assert.Equal(0m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void Saving_a_full_detail_inspection_with_no_rows_drops_an_earlier_override()
    {
        var ci = Detail(1_000_000m, 400_000m, (100m, 0m, 50m));

        ci.UpdateFullDetail(1_000_000m, null, currentValueOverride: 400_000m);
        ci.ClearWorkDetails();   // the payload carries no work rows, so no ComputeAllValues follows

        Assert.Equal(0m, ci.SummaryCurrentValue);
    }

    // ── next round ──────────────────────────────────────────────────────────────

    [Fact]
    public void Next_round_carries_an_overridden_value_into_previous_and_current()
    {
        var prior = Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m);

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Equal(966_667m, next.SummaryPreviousValue);   // not 966,700 recomputed from the percentage
        Assert.Equal(966_667m, next.SummaryCurrentValue);
        Assert.Equal(96.67m, next.SummaryPreviousProgressPct);
    }

    [Fact]
    public void Next_round_carries_an_overridden_full_detail_value()
    {
        var prior = Detail(1_000_000m, 966_667m, (100m, 90m, 96.67m));

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Equal(966_667m, next.SummaryPreviousValue);
        Assert.Equal(966_667m, next.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_on_a_pinned_zero_base_does_not_inherit_the_override()
    {
        var prior = Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m);

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid(), totalValueOverride: 0m);

        Assert.Equal(0m, next.SummaryPreviousValue);
        Assert.Equal(0m, next.SummaryCurrentValue);
    }

    [Fact]
    public void An_ordinary_resave_keeps_the_carried_previous_value()
    {
        var next = ConstructionInspection.CopyForNextInspection(
            Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m), Guid.NewGuid());

        // Previous % is read-only on the screen, so the form resends it unchanged.
        next.UpdateSummary(1_000_000m, null, 96.67m, 98m, null);

        Assert.Equal(966_667m, next.SummaryPreviousValue);
        Assert.Equal(980_000m, next.SummaryCurrentValue);
    }

    [Fact]
    public void An_ordinary_resave_of_a_full_detail_round_keeps_the_carried_previous_value()
    {
        var next = ConstructionInspection.CopyForNextInspection(
            Detail(1_000_000m, 966_667m, (100m, 90m, 96.67m)), Guid.NewGuid());

        next.UpdateFullDetail(1_000_000m, null);
        FillRows(next, (100m, 96.67m, 98m));

        Assert.Equal(966_667m, next.SummaryPreviousValue);
        Assert.Equal(980_000m, next.SummaryCurrentValue);
    }

    [Fact]
    public void A_changed_previous_percentage_recomputes_the_previous_value()
    {
        var next = ConstructionInspection.CopyForNextInspection(
            Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m), Guid.NewGuid());

        next.UpdateSummary(1_000_000m, null, 50m, 98m, null);

        Assert.Equal(500_000m, next.SummaryPreviousValue);
    }

    [Fact]
    public void A_new_base_still_moves_a_previous_value_that_was_never_carried()
    {
        var ci = Summary(1_000m, 50m, 60m);

        ci.UpdateSummary(2_000m, null, 50m, 60m, null);

        Assert.Equal(1_000m, ci.SummaryPreviousValue);
        Assert.Equal(1_200m, ci.SummaryCurrentValue);
    }

    [Fact]
    public void Switching_mode_does_not_leave_a_stale_previous_value()
    {
        var next = ConstructionInspection.CopyForNextInspection(
            Summary(1_000m, 10m, 60m, currentValueOverride: 590m), Guid.NewGuid());

        next.UpdateFullDetail(1_000m, null);
        FillRows(next, (100m, 20m, 70m));

        Assert.Equal(200m, next.SummaryPreviousValue);
        Assert.Equal(700m, next.SummaryCurrentValue);
    }

    [Fact]
    public void Percentages_are_held_to_the_column_precision_so_a_reload_does_not_flag_an_override()
    {
        var ci = Summary(1_000_000m, null, 33.33333m);

        Assert.Equal(33.3333m, ci.SummaryCurrentProgressPct);
        Assert.Equal(333_333m, ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);
    }

    [Fact]
    public void The_override_is_rounded_before_it_is_range_checked()
    {
        Assert.Equal(1_000m, Summary(1_000m, null, 50m, currentValueOverride: 1_000.4m).SummaryCurrentValue);
        Assert.Throws<DomainException>(() => Summary(1_000m, null, 50m, currentValueOverride: 1_000.5m));
    }

    [Fact]
    public void A_new_base_recomputes_a_carried_previous_value()
    {
        var next = ConstructionInspection.CopyForNextInspection(
            Summary(1_000_000m, 90m, 96.67m, currentValueOverride: 966_667m), Guid.NewGuid());

        next.UpdateSummary(2_000_000m, null, 96.67m, 98m, null);

        Assert.Equal(1_933_400m, next.SummaryPreviousValue);
    }

    [Fact]
    public void With_no_progress_keyed_there_is_nothing_to_override()
    {
        var ci = Summary(1_000m, null, null, currentValueOverride: 400m);

        Assert.Null(ci.SummaryCurrentValue);
        Assert.False(ci.IsCurrentValueOverridden);

        ci.UpdateSummary(1_000m, null, null, null, null, currentValueOverride: 400m);

        Assert.Null(ci.SummaryCurrentValue);
    }

    [Fact]
    public void Next_round_carries_the_override_on_an_unchanged_base_without_throwing()
    {
        var prior = Summary(1_000m, 10m, 60m, currentValueOverride: 1_000m);   // at the upper bound

        var next = ConstructionInspection.CopyForNextInspection(prior, Guid.NewGuid());

        Assert.Equal(1_000m, next.SummaryCurrentValue);
        Assert.Equal(1_000m, next.SummaryPreviousValue);
    }
}
