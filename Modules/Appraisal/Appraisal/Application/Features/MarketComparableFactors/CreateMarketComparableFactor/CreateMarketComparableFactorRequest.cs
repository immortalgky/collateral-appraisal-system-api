namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Request model for creating a market comparable factor.
/// </summary>
public sealed record CreateMarketComparableFactorRequest(
    string FactorCode,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    List<FactorTranslationRequest> Translations);

public sealed record FactorTranslationRequest(string Language, string FactorName);
