namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

public record AddMethodResponse(
    Guid Id,
    string MethodType,
    string Status
);
