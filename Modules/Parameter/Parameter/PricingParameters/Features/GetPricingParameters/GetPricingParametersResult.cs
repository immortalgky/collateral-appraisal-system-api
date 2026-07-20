using Parameter.Contracts.PricingParameters;

namespace Parameter.PricingParameters.Features.GetPricingParameters;

public record GetPricingParametersResult(
    List<RoomTypeDto> RoomTypes,
    List<JobPositionDto> JobPositions,
    List<TaxBracketDto> TaxBrackets,
    List<AssumptionTypeDto> AssumptionTypes,
    List<AssumptionMethodMatrixDto> AssumptionMethodMatrix,
    List<FireInsuranceRateDto> FireInsuranceRates);

public record RoomTypeDto(string Code, string Name, int DisplaySeq);

public record JobPositionDto(string Code, string Name, int DisplaySeq);

public record TaxBracketDto(int Tier, decimal TaxRate, decimal MinValue, decimal? MaxValue);

public record AssumptionTypeDto(string Code, string Name, string Category, int DisplaySeq);

public record AssumptionMethodMatrixDto(string AssumptionType, List<string> AllowedMethodCodes);

// FireInsuranceRateDto intentionally lives in Parameter.Contracts.PricingParameters so the
// cross-module GetFireInsuranceRatesQuery and this aggregate payload share one shape.
