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

        var factorDtos = factors.Select(f => new MarketComparableFactorDto(
            f.Id,
            f.FactorCode,
            f.FactorName,
            f.FieldName,
            f.DataType.ToString(),
            f.FieldLength,
            f.FieldDecimal,
            f.ParameterGroup,
            f.IsActive)).ToList();

        return new GetMarketComparableFactorsResult(factorDtos);
    }
}

/// <summary>
/// DTO for market comparable factor.
/// </summary>
public sealed record MarketComparableFactorDto(
    Guid Id,
    string FactorCode,
    string FactorName,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    bool IsActive);
