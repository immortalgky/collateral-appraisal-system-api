namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

public record UpdateApproachResponse(
    Guid Id,
    string ApproachType,
    decimal? ApproachValue,
    decimal? Weight,
    string Status
);
