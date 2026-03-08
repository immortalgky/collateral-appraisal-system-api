namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Request model for updating a market comparable factor.
/// Note: FactorCode is immutable and cannot be changed.
/// </summary>
public sealed record UpdateMarketComparableFactorRequest(
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    List<FactorTranslationRequest> Translations);

public sealed record FactorTranslationRequest(string Language, string FactorName);
