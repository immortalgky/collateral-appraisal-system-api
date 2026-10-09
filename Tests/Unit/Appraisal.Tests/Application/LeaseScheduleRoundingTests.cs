using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Application;

/// <summary>
/// The two lease-schedule figures that were still on C#'s default (banker's) rounding while the
/// screen rounded halves up. Both are derived from the same inputs on both sides, so a disagreement
/// is not a tolerance — it is two different answers to one question.
/// </summary>
public class LeaseScheduleRoundingTests
{
    // ── the remaining year fraction ──────────────────────────────────────────────────────────────
    // calculateLeasehold.ts computes `Math.round(((days360 + 1) / 360) * 10) / 10`, and its own
    // comment cites Excel's ROUND — halves up. Because 360 divides evenly, the midpoints are exact
    // and reachable: days360 + 1 ∈ {18, 90, 162, 234, 306, …} all land on x.x5.

    private static List<PricingPropertyDataService.RentalScheduleRow> OneYearContract(decimal amount)
        => [new(Year: 1,
                ContractStart: new DateTime(2026, 1, 1),
                ContractEnd: new DateTime(2026, 12, 31),
                TotalAmount: amount)];

    [Fact]
    public void A_quarter_of_a_year_remaining_rounds_up_not_to_the_even_tenth()
    {
        // DAYS360(2026-10-02, 2026-12-31) = 89, so (89 + 1) / 360 = 0.25 exactly.
        // AwayFromZero → 0.3. Banker's gave 0.2 — a tenth of a year of rent, and the same figure
        // drives the PV factors, so the whole discounting chain shifted with it.
        var rows = PricingPropertyDataService.BuildAppraisalSchedule(
            OneYearContract(1_000_000m), new DateTime(2026, 10, 2));

        var first = Assert.Single(rows);
        Assert.Equal(0.3m, first.Year);
        Assert.Equal(300_000m, first.ContractRentalFee);
    }

    [Fact]
    public void Eighteen_days_still_produce_a_row_instead_of_vanishing()
    {
        // DAYS360(2026-12-14, 2026-12-31) = 17, so (17 + 1) / 360 = 0.05 exactly.
        // Banker's rounded that to 0.0, which then failed the `fraction > 0` guard and dropped the
        // first period from the schedule entirely — while the screen still displayed it.
        var rows = PricingPropertyDataService.BuildAppraisalSchedule(
            OneYearContract(1_000_000m), new DateTime(2026, 12, 14));

        var first = Assert.Single(rows);
        Assert.Equal(0.1m, first.Year);
        Assert.Equal(100_000m, first.ContractRentalFee);
    }

    [Fact]
    public void Days360_matches_the_screens_implementation()
    {
        // days360Between in calculateLeasehold.ts is the same arithmetic; if these ever diverge the
        // fraction above is computed from different day counts and the rounding fix is moot.
        Assert.Equal(89, PricingPropertyDataService.Days360Between(new DateTime(2026, 10, 2), new DateTime(2026, 12, 31)));
        Assert.Equal(17, PricingPropertyDataService.Days360Between(new DateTime(2026, 12, 14), new DateTime(2026, 12, 31)));
        // US/NASD rule: once the start day is capped to 30, the end day is capped too — so a
        // month-end to month-end span counts as whole 30-day months rather than 61 days.
        Assert.Equal(60, PricingPropertyDataService.Days360Between(new DateTime(2026, 1, 30), new DateTime(2026, 3, 31)));
    }

    // ── the rental growth percentage ─────────────────────────────────────────────────────────────
    // RentalInfoForm.tsx derives the same figure as
    // `Math.round((growthAmount / prevBase) * 100 * 100) / 100` — halves up.

    [Fact]
    public void Growth_percent_derived_from_an_amount_rounds_a_half_satang_up()
    {
        var info = RentalInfo.Create(Guid.NewGuid());
        info.Update(
            numberOfYears: 2,
            firstYearStartDate: new DateTime(2026, 1, 1),
            contractRentalFeePerYear: 800_000m,
            growthRateType: "Property");

        // 17,000 / 800,000 × 100 = 2.125 exactly. AwayFromZero → 2.13; banker's gave 2.12.
        info.AddGrowthPeriodEntry(fromYear: 2, toYear: 2, growthRate: 0m, growthAmount: 17_000m,
            totalAmount: 817_000m);

        var rows = RentalScheduleComputer.Compute(info);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2.13m, rows[1].ContractRentalFeeGrowthRatePercent);
    }

    [Fact]
    public void An_explicit_growth_rate_is_used_as_given_and_never_re_derived()
    {
        var info = RentalInfo.Create(Guid.NewGuid());
        info.Update(
            numberOfYears: 2,
            firstYearStartDate: new DateTime(2026, 1, 1),
            contractRentalFeePerYear: 800_000m,
            growthRateType: "Property");

        // A stated rate wins; the amount-derived fallback only fires when the rate is zero.
        info.AddGrowthPeriodEntry(fromYear: 2, toYear: 2, growthRate: 5m, growthAmount: 17_000m,
            totalAmount: 840_000m);

        var rows = RentalScheduleComputer.Compute(info);

        Assert.Equal(5m, rows[1].ContractRentalFeeGrowthRatePercent);
    }
}
