using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Command to update an existing market comparable factor.
/// Note: FactorCode is immutable and cannot be changed.
/// </summary>
public sealed record UpdateMarketComparableFactorCommand(
    Guid Id,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    IReadOnlyList<(string Language, string FactorName)> Translations) : ICommand<UpdateMarketComparableFactorResult>;
