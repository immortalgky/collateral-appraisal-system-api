using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Command to update an existing market comparable factor.
/// Note: FactorCode is immutable and cannot be changed.
/// </summary>
public sealed record UpdateMarketComparableFactorCommand(
    Guid Id,
    string FactorName,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup) : ICommand<UpdateMarketComparableFactorResult>;
