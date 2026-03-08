using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Command to create a new market comparable factor.
/// </summary>
public sealed record CreateMarketComparableFactorCommand(
    string FactorCode,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    IReadOnlyList<(string Language, string FactorName)> Translations) : ICommand<CreateMarketComparableFactorResult>;
