namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

public record UpdateMethodResponse(
    Guid Id,
    string MethodType,
    decimal? MethodValue,
    decimal? ValuePerUnit,
    string? UnitType,
    string Status
);
