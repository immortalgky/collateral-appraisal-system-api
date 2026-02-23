namespace Appraisal.Application.Features.PricingAnalysis.AddMethod;

/// <summary>
/// Result of adding a method to an approach
/// </summary>
public record AddMethodResult(
    Guid Id,
    string MethodType
);
