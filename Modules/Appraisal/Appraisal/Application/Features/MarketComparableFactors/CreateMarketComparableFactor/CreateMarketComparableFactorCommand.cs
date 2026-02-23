using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Command to create a new market comparable factor.
/// </summary>
public sealed record CreateMarketComparableFactorCommand(
    string FactorCode,
    string FactorName,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup) : ICommand<CreateMarketComparableFactorResult>;
