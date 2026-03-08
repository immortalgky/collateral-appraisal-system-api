namespace Appraisal.Domain.Appraisals;

/// <summary>
/// RSQ (R-Squared) linear regression result for WQS pricing method.
/// </summary>
public class PricingRsqResult : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }

    public decimal? CoefficientOfDecision { get; private set; } // R²
    public decimal? StandardError { get; private set; }
    public decimal? IntersectionPoint { get; private set; } // Intercept
    public decimal? Slope { get; private set; }
    public decimal? RsqFinalValue { get; private set; }
    public decimal? LowestEstimate { get; private set; }
    public decimal? HighestEstimate { get; private set; }

    private PricingRsqResult() { }

    public static PricingRsqResult Create(Guid pricingMethodId)
    {
        return new PricingRsqResult
        {
            PricingMethodId = pricingMethodId
        };
    }

    public void Update(
        decimal? coefficientOfDecision,
        decimal? standardError,
        decimal? intersectionPoint,
        decimal? slope,
        decimal? rsqFinalValue,
        decimal? lowestEstimate,
        decimal? highestEstimate)
    {
        CoefficientOfDecision = coefficientOfDecision;
        StandardError = standardError;
        IntersectionPoint = intersectionPoint;
        Slope = slope;
        RsqFinalValue = rsqFinalValue;
        LowestEstimate = lowestEstimate;
        HighestEstimate = highestEstimate;
    }
}
