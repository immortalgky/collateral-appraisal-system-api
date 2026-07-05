namespace Appraisal.Application.Features.PricingAnalysis.SelectApproach;

public record SelectApproachResult(
    Guid Id,
    string ApproachType,
    decimal? FinalAppraisedValue
);
