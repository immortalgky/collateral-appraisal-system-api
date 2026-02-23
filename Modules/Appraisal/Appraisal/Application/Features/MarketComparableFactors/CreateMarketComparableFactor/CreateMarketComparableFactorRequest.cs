namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Request model for creating a market comparable factor.
/// </summary>
public sealed record CreateMarketComparableFactorRequest(
    string FactorCode,
    string FactorName,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup);
