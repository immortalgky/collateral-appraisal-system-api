namespace Appraisal.Application.Features.PricingAnalysis.UpdateComparableLink;

public record UpdateComparableLinkResponse(Guid LinkId, decimal? Weight, int DisplaySequence);
