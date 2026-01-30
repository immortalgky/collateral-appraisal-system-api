namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

public record UpdateMethodRequest(
    decimal? MethodValue = null,
    decimal? ValuePerUnit = null,
    string? UnitType = null
);
