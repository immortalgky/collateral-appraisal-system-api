namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Links pricing method to market comparables used in comparison.
/// </summary>
public class PricingComparableLink : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid MarketComparableId { get; private set; }
    public int DisplaySequence { get; private set; }

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
            // Id = Guid.NewGuid(),
            PricingMethodId = pricingMethodId,
            MarketComparableId = marketComparableId,
            DisplaySequence = displaySequence
        };
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }
}