using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// WQS (Weight Quality Score) calculation service.
/// Recalculates: WeightedScores, Summations, InitialPrices, RSQ linear regression.
/// </summary>
public class WqsCalculationService : IPricingCalculationService
{
    public void Recalculate(PricingAnalysisMethod method)
    {
        // Step 1: Compute initial price per survey calculation
        var dataPoints = new List<(decimal summation, decimal adjustedPrice)>();

        foreach (var calc in method.Calculations)
        {
            var initialPrice = PricingCalculationHelper.ComputeInitialPrice(calc);
            if (initialPrice.HasValue)
                calc.SetResult(initialPrice.Value);

            // Compute summation for this comparable
            var summation = method.GetFactorScoresForComparable(calc.MarketComparableId)
                .Where(f => f.WeightedScore.HasValue)
                .Sum(f => f.WeightedScore!.Value);

            if (initialPrice.HasValue && summation != 0)
                dataPoints.Add((summation, initialPrice.Value));
        }

        // Compute collateral summation
        var collateralSummation = method.GetFactorScoresForComparable(null)
            .Where(f => f.WeightedScore.HasValue)
            .Sum(f => f.WeightedScore!.Value);

        // Step 2: RSQ linear regression (need at least 2 data points)
        if (dataPoints.Count >= 2)
        {
            var rsq = ComputeLinearRegression(dataPoints, collateralSummation);

            // Step 3: Auto-compute PricingFinalValue from freshly computed RSQ result
            var fv = rsq.FinalValue;
            var fvRounded = Math.Floor(fv / 10_000m) * 10_000m;

            if (method.FinalValue is null)
            {
                var finalValue = PricingFinalValue.Create(method.Id, fv, fvRounded);
                method.SetFinalValue(finalValue);
            }
            else
            {
                method.FinalValue.UpdateFinalValue(fv, fvRounded);
            }

            method.SetValue(fvRounded);

            // Use user's FinalValueRounded (if set) for lowest/highest center
            var centerValue = (double)method.FinalValue!.FinalValueRounded;
            var lowest = (decimal)(centerValue - (double)rsq.StandardError);
            var highest = (decimal)(centerValue + (double)rsq.StandardError);

            if (method.RsqResult is null)
            {
                var rsqResult = PricingRsqResult.Create(method.Id);
                rsqResult.Update(
                    rsq.R2, rsq.StandardError, rsq.Intercept, rsq.Slope,
                    rsq.FinalValue, lowest, highest);
                method.SetRsqResult(rsqResult);
            }
            else
            {
                method.RsqResult.Update(
                    rsq.R2, rsq.StandardError, rsq.Intercept, rsq.Slope,
                    rsq.FinalValue, lowest, highest);
            }
        }
    }

    private static RsqValues ComputeLinearRegression(
        List<(decimal summation, decimal adjustedPrice)> points,
        decimal collateralSummation)
    {
        int n = points.Count;

        // Convert to doubles for regression math
        double[] x = points.Select(p => (double)p.summation).ToArray();
        double[] y = points.Select(p => (double)p.adjustedPrice).ToArray();

        double sumX = x.Sum();
        double sumY = y.Sum();
        double sumXY = x.Zip(y, (a, b) => a * b).Sum();
        double sumX2 = x.Sum(v => v * v);
        double sumY2 = y.Sum(v => v * v);

        double meanX = sumX / n;
        double meanY = sumY / n;

        // Slope and intercept
        double denominator = sumX2 - n * meanX * meanX;
        double slope = denominator == 0 ? 0 : (sumXY - n * meanX * meanY) / denominator;
        double intercept = meanY - slope * meanX;

        // R² (coefficient of determination)
        double ssTot = sumY2 - n * meanY * meanY;
        double ssRes = 0;
        for (int i = 0; i < n; i++)
        {
            double predicted = intercept + slope * x[i];
            double residual = y[i] - predicted;
            ssRes += residual * residual;
        }
        double r2 = ssTot == 0 ? 0 : 1 - ssRes / ssTot;

        // Standard error of the estimate
        int df = n - 2; // degrees of freedom
        double standardError = df > 0 ? Math.Sqrt(ssRes / df) : 0;

        // Final value using collateral summation
        double finalValue = intercept + slope * (double)collateralSummation;

        return new RsqValues(
            (decimal)r2,
            (decimal)standardError,
            (decimal)intercept,
            (decimal)slope,
            (decimal)finalValue);
    }

    private record RsqValues(
        decimal R2,
        decimal StandardError,
        decimal Intercept,
        decimal Slope,
        decimal FinalValue);
}
