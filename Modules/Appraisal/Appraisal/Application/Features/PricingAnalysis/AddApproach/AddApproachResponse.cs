namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

public record AddApproachResponse(
    Guid Id,
    string ApproachType,
    decimal? Weight,
    string Status
);
