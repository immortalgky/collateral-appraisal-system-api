namespace Appraisal.Application.Features.PricingAnalysis.UpdateComparableLink;

public record UpdateComparableLinkRequest(
    decimal? Weight = null,
    int? DisplaySequence = null
);
