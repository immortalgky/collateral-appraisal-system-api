using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Server-side calculation for leasehold pricing analysis.
/// Mirrors the frontend calculateLeasehold.ts logic exactly.
/// </summary>
public class LeaseholdCalculationService
{
    public record RentalScheduleRow(int Year, DateTime ContractStart, DateTime ContractEnd, decimal TotalAmount);

    /// <summary>
    /// Appraisal schedule row — re-indexed from appraisal date using DAYS360.
    /// </summary>
    public record AppraisalScheduleRow(decimal Year, decimal TotalAmount);

    public record TableRowResult(
        decimal Year,
        decimal LandValue,
        decimal LandGrowthPercent,
        decimal BuildingValue,
        decimal DepreciationAmount,
        decimal DepreciationPercent,
        decimal BuildingAfterDepreciation,
        decimal TotalLandAndBuilding,
        decimal RentalIncome,
        decimal PvFactor,
        decimal NetCurrentRentalIncome);

    public record LeaseholdCalcResult(
        List<TableRowResult> Rows,
        decimal TotalIncomeOverLeaseTerm,
        decimal ValueAtLeaseExpiry,
        decimal FinalValue,
        decimal FinalValueRounded);

    /// <summary>
    /// Calculates the leasehold analysis values from inputs and appraisal schedule.
    /// Uses the same running building calculation as the frontend.
    /// </summary>
    public LeaseholdCalcResult Calculate(
        LeaseholdAnalysis analysis,
        IReadOnlyList<AppraisalScheduleRow> appraisalSchedule,
        decimal totalLandAreaInSqWa)
    {
        if (appraisalSchedule.Count == 0)
            return new LeaseholdCalcResult([], 0, 0, 0, 0);

        var discountRate = analysis.DiscountRate / 100m;
        var costIndex = analysis.ConstructionCostIndex / 100m;
        var depRate = analysis.DepreciationRate / 100m;
        var depInterval = analysis.DepreciationIntervalYears > 0 ? analysis.DepreciationIntervalYears : 1;
        var baseLandValue = analysis.LandValuePerSqWa * totalLandAreaInSqWa;
        var initialBuildingValue = analysis.InitialBuildingValue;

        var tableRows = new List<TableRowResult>();
        decimal totalNetRentalIncome = 0;
        decimal prevBuildingValue = 0;
        decimal prevDepAmount = 0;
        decimal lastTotalLandAndBuilding = 0;
        decimal lastPvFactor = 1;
        decimal prevLandValue = 0;

        for (int i = 0; i < appraisalSchedule.Count; i++)
        {
            var row = appraisalSchedule[i];
            var year = row.Year;

            // Land value with growth
            var landValue = CalculateLandValueGrowth(analysis, baseLandValue, year);

            // Building value: starts from buildingCalcStartYear (1-based column index)
            var showBuilding = initialBuildingValue > 0 && (i + 1) >= analysis.BuildingCalcStartYear;
            var isFirstBuildingYear = showBuilding && (i + 1) == analysis.BuildingCalcStartYear;

            decimal buildingValue;
            if (!showBuilding)
            {
                buildingValue = 0;
            }
            else if (isFirstBuildingYear)
            {
                buildingValue = initialBuildingValue;
            }
            else
            {
                // (prevBuildingValue × (1 + costIndex%)) - prevDepreciationAmount
                buildingValue = Math.Round(prevBuildingValue * (1 + costIndex) - prevDepAmount, 2);
            }

            // Depreciation: flat rate applied every depInterval years
            var isDepYear = showBuilding && Math.Floor(year / depInterval) > Math.Floor((year - 1) / depInterval);
            var depAmount = isDepYear ? Math.Round(buildingValue * depRate, 2) : 0m;
            var buildingAfterDep = showBuilding ? Math.Round(buildingValue - depAmount, 2) : 0m;

            // Update running values
            if (showBuilding)
            {
                prevBuildingValue = buildingValue;
                prevDepAmount = depAmount;
            }

            var totalLandAndBuilding = Math.Round(landValue + buildingAfterDep, 2);

            // PV factor and rental income
            var pvFactor = CalculatePvFactor(discountRate, year);
            var rentalIncome = row.TotalAmount;
            var netCurrentRentalIncome = Math.Round(rentalIncome * pvFactor, 2);
            totalNetRentalIncome += netCurrentRentalIncome;

            // Land growth %
            decimal landGrowthPercent = 0;
            if (i > 0 && prevLandValue > 0)
                landGrowthPercent = Math.Round((landValue - prevLandValue) / prevLandValue * 100, 2);
            prevLandValue = landValue;

            // Depreciation %
            var depPercent = isDepYear ? analysis.DepreciationRate : 0m;

            tableRows.Add(new TableRowResult(
                year, landValue, landGrowthPercent,
                buildingValue, depAmount, depPercent, buildingAfterDep,
                totalLandAndBuilding, rentalIncome, pvFactor, netCurrentRentalIncome));

            // Track last row values for value at expiry
            lastTotalLandAndBuilding = totalLandAndBuilding;
            lastPvFactor = pvFactor;
        }

        var valueAtLeaseExpiry = Math.Round(lastTotalLandAndBuilding * lastPvFactor, 2);
        var finalValue = Math.Round(totalNetRentalIncome + valueAtLeaseExpiry, 2);
        var finalValueRounded = Math.Round(finalValue / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        return new LeaseholdCalcResult(
            tableRows,
            totalNetRentalIncome,
            valueAtLeaseExpiry,
            finalValue,
            finalValueRounded);
    }

    /// <summary>
    /// Calculates partial usage deductions.
    /// </summary>
    public static (decimal? PartialLandArea, decimal? PartialLandPrice, decimal? EstimateNetPrice, decimal? EstimatePriceRounded)
        CalculatePartialUsage(
            decimal finalValueRounded,
            decimal? partialRai,
            decimal? partialNgan,
            decimal? partialWa,
            decimal? pricePerSqWa)
    {
        var partialLandArea = (partialRai ?? 0) * 400m + (partialNgan ?? 0) * 100m + (partialWa ?? 0);
        if (partialLandArea == 0 || !pricePerSqWa.HasValue)
            return (partialLandArea > 0 ? partialLandArea : null, null, null, null);

        var partialLandPrice = pricePerSqWa.Value * partialLandArea;
        var estimateNetPrice = finalValueRounded + partialLandPrice;
        var estimatePriceRounded = Math.Round(estimateNetPrice / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        return (partialLandArea, Math.Round(partialLandPrice, 2), Math.Round(estimateNetPrice, 2), estimatePriceRounded);
    }

    private static decimal CalculatePvFactor(decimal discountRate, decimal year)
    {
        if (discountRate == 0) return 1m;
        return 1m / (decimal)Math.Pow((double)(1m + discountRate), (double)year);
    }

    private static decimal CalculateLandValueGrowth(LeaseholdAnalysis analysis, decimal baseLandValue, decimal year)
    {
        if (baseLandValue == 0) return 0;

        if (analysis.LandGrowthRateType == "Frequency")
        {
            var rate = analysis.LandGrowthRatePercent / 100m;
            var interval = analysis.LandGrowthIntervalYears;
            if (interval <= 0) return baseLandValue;

            var periods = (int)Math.Floor(year / interval);
            return Math.Round(baseLandValue * (decimal)Math.Pow((double)(1m + rate), periods), 2);
        }
        else // "Period" — iterate year by year (matches frontend logic)
        {
            var currentValue = baseLandValue;
            for (int y = 1; y <= (int)Math.Floor(year); y++)
            {
                var period = analysis.LandGrowthPeriods
                    .OrderBy(p => p.FromYear)
                    .FirstOrDefault(p => y >= p.FromYear && y <= p.ToYear);
                var rate = period != null ? period.GrowthRatePercent / 100m : 0m;
                currentValue *= (1m + rate);
            }
            return Math.Round(currentValue, 2);
        }
    }
}
