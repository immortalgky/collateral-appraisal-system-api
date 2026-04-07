using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Computes the rental schedule from RentalInfo fields.
/// Single source of truth for schedule calculation — used by both query and save handlers.
/// </summary>
public static class RentalScheduleComputer
{
    public record ComputedScheduleEntry(
        int Year, DateTime ContractStart, DateTime ContractEnd,
        decimal UpFront, decimal ContractRentalFee, decimal TotalAmount,
        decimal ContractRentalFeeGrowthRatePercent);

    public static List<ComputedScheduleEntry> Compute(RentalInfo info)
    {
        var rows = new List<ComputedScheduleEntry>();
        if (info.NumberOfYears <= 0 || info.FirstYearStartDate is null)
            return rows;

        var startDate = info.FirstYearStartDate.Value;
        var currentFee = info.ContractRentalFeePerYear;

        for (var year = 1; year <= info.NumberOfYears; year++)
        {
            var contractStart = startDate.AddYears(year - 1);
            var contractEnd = startDate.AddYears(year).AddDays(-1);

            var upFront = info.UpFrontEntries
                .Where(e => e.AtYear >= contractStart && e.AtYear <= contractEnd)
                .Sum(e => e.UpFrontAmount);

            decimal growthRatePercent = 0;

            // Frequency growth: apply every N years (skip year 1)
            if (year > 1 && info.GrowthRateType == "Period")
            {
                if (info.GrowthIntervalYears > 0 && (year - 1) % info.GrowthIntervalYears == 0)
                {
                    growthRatePercent = info.GrowthRatePercent;
                    currentFee += currentFee * growthRatePercent / 100m;
                }
            }

            // Period growth: apply from fromYear (no base year skip)
            if (info.GrowthRateType == "Property")
            {
                var sortedEntries = info.GrowthPeriodEntries.OrderBy(e => e.FromYear).ToList();
                var entryIdx = sortedEntries.FindIndex(e => year >= e.FromYear && year <= e.ToYear);
                var periodEntry = entryIdx >= 0 ? sortedEntries[entryIdx] : null;

                if (periodEntry is not null)
                {
                    currentFee = periodEntry.TotalAmount;
                    if (year == periodEntry.FromYear)
                    {
                        growthRatePercent = periodEntry.GrowthRate;
                        if (growthRatePercent == 0 && periodEntry.GrowthAmount > 0)
                        {
                            var prevBase = entryIdx > 0
                                ? sortedEntries[entryIdx - 1].TotalAmount
                                : info.ContractRentalFeePerYear;
                            if (prevBase > 0)
                            {
                                growthRatePercent = Math.Round(periodEntry.GrowthAmount / prevBase * 100m, 2);
                            }
                        }
                    }
                }
            }

            var totalAmount = currentFee + upFront;

            rows.Add(new ComputedScheduleEntry(
                Year: year,
                ContractStart: contractStart,
                ContractEnd: contractEnd,
                UpFront: upFront,
                ContractRentalFee: currentFee,
                TotalAmount: totalAmount,
                ContractRentalFeeGrowthRatePercent: growthRatePercent));
        }

        return rows;
    }

    /// <summary>
    /// Compute schedule and apply overrides, then save entries to the RentalInfo aggregate.
    /// </summary>
    public static void ComputeAndSave(RentalInfo info, List<RentalScheduleOverrideData>? overrides)
    {
        var computed = Compute(info);

        info.ClearScheduleEntries();
        foreach (var row in computed)
        {
            var upFront = row.UpFront;
            var fee = row.ContractRentalFee;

            // Apply overrides if provided
            if (overrides is not null)
            {
                var ovr = overrides.FirstOrDefault(o => o.Year == row.Year);
                if (ovr is not null)
                {
                    if (ovr.UpFront.HasValue) upFront = ovr.UpFront.Value;
                    if (ovr.ContractRentalFee.HasValue) fee = ovr.ContractRentalFee.Value;
                }
            }

            info.AddScheduleEntry(row.Year, row.ContractStart, row.ContractEnd,
                upFront, fee, upFront + fee, row.ContractRentalFeeGrowthRatePercent);
        }

        // Save overrides
        info.ClearScheduleOverrides();
        if (overrides is not null)
        {
            foreach (var ovr in overrides)
                info.SetScheduleOverride(ovr.Year, ovr.UpFront, ovr.ContractRentalFee);
        }
    }
}
