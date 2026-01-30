namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

/// <summary>
/// Result of updating a method
/// </summary>
public record UpdateMethodResult(
    Guid Id,
    string MethodType,
    decimal? MethodValue,
    decimal? ValuePerUnit,
    string? UnitType
);
