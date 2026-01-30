namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Request model for updating a market comparable factor.
/// Note: FactorCode and DataType are immutable and cannot be changed.
/// </summary>
public sealed record UpdateMarketComparableFactorRequest(
    string FactorName,
    string FieldName,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup);
