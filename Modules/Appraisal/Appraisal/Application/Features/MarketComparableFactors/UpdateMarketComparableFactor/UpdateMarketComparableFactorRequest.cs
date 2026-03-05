namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Request model for updating a market comparable factor.
/// Note: FactorCode is immutable and cannot be changed.
/// </summary>
public sealed record UpdateMarketComparableFactorRequest(
    string FactorName,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup);
