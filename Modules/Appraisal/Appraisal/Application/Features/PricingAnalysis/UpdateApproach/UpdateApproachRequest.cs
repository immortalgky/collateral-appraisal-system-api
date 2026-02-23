namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

public record UpdateApproachRequest(
    decimal? ApproachValue = null,
    decimal? Weight = null
);
