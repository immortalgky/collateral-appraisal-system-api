using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.GetMarketComparableFactors;

/// <summary>
/// Handles retrieval of all market comparable factors.
/// </summary>
internal sealed class GetMarketComparableFactorsQueryHandler :
    IQueryHandler<GetMarketComparableFactorsQuery, GetMarketComparableFactorsResult>
{
    private readonly IMarketComparableFactorRepository _repository;

    public GetMarketComparableFactorsQueryHandler(IMarketComparableFactorRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetMarketComparableFactorsResult> Handle(
        GetMarketComparableFactorsQuery query,
        CancellationToken cancellationToken)
    {
        var factors = await _repository.GetAllAsync(query.ActiveOnly, cancellationToken);

        var factorDtos = factors.Select(f =>
        {
            var translations = f.Translations.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Language))
            {
                var match = f.Translations.FirstOrDefault(
                    t => t.Language.Equals(query.Language, StringComparison.OrdinalIgnoreCase));

                translations = match is not null
                    ? [match]
                    : f.Translations.Where(t => t.Language.Equals("en", StringComparison.OrdinalIgnoreCase));
            }

            return new MarketComparableFactorDto(
                f.Id,
                f.FactorCode,
                f.FieldName,
                f.DataType.ToString(),
                f.FieldLength,
                f.FieldDecimal,
                f.ParameterGroup,
                f.IsActive,
                translations.Select(t => new FactorTranslationDto(t.Language, t.FactorName)).ToList());
        }).ToList();

        return new GetMarketComparableFactorsResult(factorDtos);
    }
}

public sealed record MarketComparableFactorDto(
    Guid Id,
    string FactorCode,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    bool IsActive,
    IReadOnlyList<FactorTranslationDto> Translations);

public sealed record FactorTranslationDto(string Language, string FactorName);
