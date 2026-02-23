namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

public record SelectMethodResponse(
    Guid Id,
    string MethodType,
    string Status
);
