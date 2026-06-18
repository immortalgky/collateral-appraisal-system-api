namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// API response for the pricing-analysis pre-flight validation.
/// Status is serialised as its string name (e.g. "Passed") for the front-end.
/// </summary>
public record ValidateGroupForPricingResponse(
    bool Valid,
    IReadOnlyList<PricingValidationStepResponse> Steps
);

public record PricingValidationStepResponse(
    string Key,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Messages
);
