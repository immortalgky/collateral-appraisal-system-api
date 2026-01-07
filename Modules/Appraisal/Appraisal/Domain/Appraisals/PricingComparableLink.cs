namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Links pricing method to market comparables used in comparison.
/// </summary>
public class PricingComparableLink : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid MarketComparableId { get; private set; }
    public int DisplaySequence { get; private set; }
    public decimal? Weight { get; private set; }

    private PricingComparableLink()
    {
    }

    public static PricingComparableLink Create(
        Guid pricingMethodId,
        Guid marketComparableId,
        int displaySequence,
        decimal? weight = null)
    {
        return new PricingComparableLink
        {
            Id = Guid.NewGuid(),
            PricingMethodId = pricingMethodId,
            MarketComparableId = marketComparableId,
            DisplaySequence = displaySequence,
            Weight = weight
        };
    }

    public void SetWeight(decimal weight)
    {
        if (weight < 0 || weight > 100)
            throw new ArgumentException("Weight must be between 0 and 100");

        Weight = weight;
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }
}