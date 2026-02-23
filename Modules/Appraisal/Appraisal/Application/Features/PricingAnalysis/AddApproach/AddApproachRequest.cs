namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

public record AddApproachRequest(
    string ApproachType,
    decimal? Weight = null
);
