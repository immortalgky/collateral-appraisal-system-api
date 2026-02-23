namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public record LinkComparableRequest(
    Guid MarketComparableId,
    int DisplaySequence,
    decimal? Weight = null
);
