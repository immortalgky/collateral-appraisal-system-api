namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

/// <summary>
/// Result of selecting a method
/// </summary>
public record SelectMethodResult(
    Guid Id,
    string MethodType
);
