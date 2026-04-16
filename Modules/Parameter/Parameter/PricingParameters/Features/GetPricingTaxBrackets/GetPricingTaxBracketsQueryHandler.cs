using Parameter.Contracts.PricingParameters;

namespace Parameter.PricingParameters.Features.GetPricingTaxBrackets;

/// <summary>
/// Handles <see cref="GetPricingTaxBracketsQuery"/> — fetches the seeded property-tax brackets
/// from the database for use by the Appraisal module's Method-10 server-side derivation.
/// </summary>
public class GetPricingTaxBracketsQueryHandler(
    ParameterDbContext context
) : MediatR.IRequestHandler<GetPricingTaxBracketsQuery, GetPricingTaxBracketsResult>
{
    public async Task<GetPricingTaxBracketsResult> Handle(
        GetPricingTaxBracketsQuery request,
        CancellationToken cancellationToken)
    {
        var brackets = await context.PricingParameterTaxBrackets
            .OrderBy(t => t.Tier)
            .Select(t => new TaxBracketDto(t.Tier, t.TaxRate, t.MinValue, t.MaxValue))
            .ToListAsync(cancellationToken);

        return new GetPricingTaxBracketsResult(brackets);
    }
}
