using MediatR;

namespace Parameter.Contracts.PricingParameters;

/// <summary>
/// Cross-module query: returns the seeded property-tax brackets used in Method-10 server-side derivation.
/// Handler lives in Parameter module; Appraisal module dispatches via MediatR.
/// </summary>
public record GetPricingTaxBracketsQuery() : IRequest<GetPricingTaxBracketsResult>;

public record GetPricingTaxBracketsResult(IReadOnlyList<TaxBracketDto> Brackets);

/// <summary>
/// A single property-tax bracket.
/// <c>TaxRate</c> is a decimal fraction (0.02 = 2%). <c>MaxValue</c> is null for the top tier (no upper bound).
/// </summary>
public record TaxBracketDto(int Tier, decimal TaxRate, decimal MinValue, decimal? MaxValue);
