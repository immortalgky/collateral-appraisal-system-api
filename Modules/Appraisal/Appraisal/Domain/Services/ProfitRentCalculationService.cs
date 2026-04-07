using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Server-side calculation for profit rent pricing analysis.
/// Calculates the PV of (market rent - contract rent) over the lease term.
/// </summary>
public class ProfitRentCalculationService
{
    public record AppraisalScheduleRow(decimal Year, decimal NumberOfMonths, decimal ContractRentalFee);

    public record TableRowResult(
        decimal Year,
        decimal NumberOfMonths,
        decimal MarketRentalFeePerSqWa,
        decimal MarketRentalFeeGrowthPercent,
        decimal MarketRentalFeePerMonth,
        decimal MarketRentalFeePerYear,
        decimal ContractRentalFeePerYear,
        decimal ReturnsFromLease,
        decimal PvFactor,
        decimal PresentValue);

    public record ProfitRentCalcResult(
        List<TableRowResult> Rows,
        decimal TotalMarketRentalFee,
        decimal TotalContractRentalFee,
        decimal TotalReturnsFromLease,
        decimal TotalPresentValue,
        decimal FinalValueRounded);

    public ProfitRentCalcResult Calculate(
        ProfitRentAnalysis analysis,
        IReadOnlyList<AppraisalScheduleRow> schedule,
        decimal totalLandAreaSqWa)
    {
        if (schedule.Count == 0)
            return new ProfitRentCalcResult([], 0, 0, 0, 0, 0);

        var discountRate = analysis.DiscountRate / 100m;
        var baseRentalFee = analysis.MarketRentalFeePerSqWa;

        var rows = new List<TableRowResult>();
        decimal totalMarketFee = 0;
        decimal totalContractFee = 0;
        decimal totalReturns = 0;
        decimal totalPresentValue = 0;
        decimal prevFeePerSqWa = 0;

        for (int i = 0; i < schedule.Count; i++)
        {
            var row = schedule[i];

            // Calculate market rental fee per SqWa with growth
            var feePerSqWa = CalculateMarketFeeGrowth(analysis, baseRentalFee, row.Year);

            // Growth percent
            decimal growthPercent = 0;
            if (i > 0 && prevFeePerSqWa > 0)
                growthPercent = Math.Round((feePerSqWa - prevFeePerSqWa) / prevFeePerSqWa * 100, 2);
            prevFeePerSqWa = feePerSqWa;

            // Market fee per month = fee per SqWa * land area
            var marketFeePerMonth = Math.Round(feePerSqWa * totalLandAreaSqWa, 2);

            // Market fee per year = fee per month * number of months
            var marketFeePerYear = Math.Round(marketFeePerMonth * row.NumberOfMonths, 2);

            // Contract rental fee from schedule
            var contractFeePerYear = row.ContractRentalFee;

            // Returns from lease = market - contract
            var returnsFromLease = Math.Round(marketFeePerYear - contractFeePerYear, 2);

            // PV factor
            var pvFactor = CalculatePvFactor(discountRate, row.Year);

            // Present value
            var presentValue = Math.Round(returnsFromLease * pvFactor, 2);

            totalMarketFee += marketFeePerYear;
            totalContractFee += contractFeePerYear;
            totalReturns += returnsFromLease;
            totalPresentValue += presentValue;

            rows.Add(new TableRowResult(
                row.Year, row.NumberOfMonths, feePerSqWa, growthPercent,
                marketFeePerMonth, marketFeePerYear, contractFeePerYear,
                returnsFromLease, pvFactor, presentValue));
        }

        var finalValueRounded = Math.Round(totalPresentValue / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        return new ProfitRentCalcResult(
            rows,
            Math.Round(totalMarketFee, 2),
            Math.Round(totalContractFee, 2),
            Math.Round(totalReturns, 2),
            Math.Round(totalPresentValue, 2),
            finalValueRounded);
    }

    private static decimal CalculatePvFactor(decimal discountRate, decimal year)
    {
        if (discountRate == 0) return 1m;
        return 1m / (decimal)Math.Pow((double)(1m + discountRate), (double)year);
    }

    private static decimal CalculateMarketFeeGrowth(ProfitRentAnalysis analysis, decimal baseFee, decimal year)
    {
        if (baseFee == 0) return 0;

        if (analysis.GrowthRateType == "Frequency")
        {
            var rate = analysis.GrowthRatePercent / 100m;
            var interval = analysis.GrowthIntervalYears;
            if (interval <= 0) return baseFee;

            var periods = (int)Math.Floor(year / interval);
            return Math.Round(baseFee * (decimal)Math.Pow((double)(1m + rate), periods), 2);
        }
        else // "Period"
        {
            var sortedPeriods = analysis.GrowthPeriods.OrderBy(p => p.FromYear).ToList();
            var currentValue = baseFee;
            for (int y = 1; y <= (int)Math.Floor(year); y++)
            {
                var period = sortedPeriods.FirstOrDefault(p => y >= p.FromYear && y <= p.ToYear);
                var rate = period != null ? period.GrowthRatePercent / 100m : 0m;
                currentValue *= (1m + rate);
            }
            return Math.Round(currentValue, 2);
        }
    }
}
