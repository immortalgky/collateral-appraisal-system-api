namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

public record AddMethodRequest(
    string MethodType,
    string? Status = null
);
