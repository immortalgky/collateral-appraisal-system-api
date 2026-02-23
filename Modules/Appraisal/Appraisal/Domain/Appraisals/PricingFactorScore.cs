namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Step 2 factor scoring for WQS and SaleGrid pricing methods.
/// Moved from PricingCalculation to PricingAnalysisMethod level.
/// Each row represents a factor score for a specific comparable (or collateral when MarketComparableId is null).
/// </summary>
public class PricingFactorScore : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid? MarketComparableId { get; private set; } // null = Collateral, GUID = Survey
    public Guid FactorId { get; private set; }

    // Factor Configuration
    public decimal FactorWeight { get; private set; }
    public int DisplaySequence { get; private set; }

    // Value and Score (unified - context determined by MarketComparableId)
    public string? Value { get; private set; }
    public decimal? Score { get; private set; }
    public decimal? WeightedScore { get; private set; }
    public decimal? AdjustmentPct { get; private set; }

    public string? Remarks { get; private set; }

    private PricingFactorScore()
    {
    }

    public static PricingFactorScore Create(
        Guid pricingMethodId,
        Guid factorId,
        decimal factorWeight,
        int displaySequence,
        Guid? marketComparableId = null)
    {
        if (factorWeight < 0 || factorWeight > 100)
            throw new ArgumentException("FactorWeight must be between 0 and 100");

        return new PricingFactorScore
        {
            PricingMethodId = pricingMethodId,
            MarketComparableId = marketComparableId,
            FactorId = factorId,
            FactorWeight = factorWeight,
            DisplaySequence = displaySequence
        };
    }

    public void SetValues(string? value, decimal? score)
    {
        Value = value;
        Score = score;
        CalculateWeightedScore();
    }

    public void SetAdjustment(decimal? adjustmentPct, string? remarks = null)
    {
        AdjustmentPct = adjustmentPct;
        Remarks = remarks;
    }

    public void UpdateWeight(decimal weight)
    {
        if (weight < 0 || weight > 100)
            throw new ArgumentException("Weight must be between 0 and 100");

        FactorWeight = weight;
        CalculateWeightedScore();
    }

    public void UpdateSequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    private void CalculateWeightedScore()
    {
        if (Score.HasValue)
            WeightedScore = Score.Value * (FactorWeight / 100m);
        else
            WeightedScore = null;
    }

    /// <summary>
    /// Updates all properties at once
    /// </summary>
    public void Update(
        decimal factorWeight,
        int displaySequence,
        string? value,
        decimal? score,
        decimal? adjustmentPct,
        string? remarks)
    {
        if (factorWeight < 0 || factorWeight > 100)
            throw new ArgumentException("FactorWeight must be between 0 and 100");

        FactorWeight = factorWeight;
        DisplaySequence = displaySequence;
        Value = value;
        Score = score;
        AdjustmentPct = adjustmentPct;
        Remarks = remarks;
        CalculateWeightedScore();
    }
}