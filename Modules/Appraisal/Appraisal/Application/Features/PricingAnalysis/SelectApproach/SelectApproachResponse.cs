namespace Appraisal.Application.Features.PricingAnalysis.SelectApproach;

public record SelectApproachResponse(
    Guid Id,
    string ApproachType,
    decimal? FinalAppraisedValue
);
