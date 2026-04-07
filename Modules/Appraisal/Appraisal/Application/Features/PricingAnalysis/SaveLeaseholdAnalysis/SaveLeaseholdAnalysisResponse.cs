namespace Appraisal.Application.Features.PricingAnalysis.SaveLeaseholdAnalysis;

public record SaveLeaseholdAnalysisResponse(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal TotalIncomeOverLeaseTerm,
    decimal ValueAtLeaseExpiry,
    decimal FinalValue,
    decimal FinalValueRounded
);
