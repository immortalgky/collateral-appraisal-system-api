using Parameter.Contracts.PricingParameters;

namespace Parameter.PricingParameters.Features.GetFireInsuranceRates;

/// <summary>
/// Handles <see cref="GetFireInsuranceRatesQuery"/> — fetches the seeded fire-insurance coverage
/// rates from the database for use by the Appraisal module's insurance-coverage derivation.
/// </summary>
public class GetFireInsuranceRatesQueryHandler(
    ParameterDbContext context
) : MediatR.IRequestHandler<GetFireInsuranceRatesQuery, GetFireInsuranceRatesResult>
{
    public async Task<GetFireInsuranceRatesResult> Handle(
        GetFireInsuranceRatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PricingParameterFireInsuranceRates.AsQueryable();

        if (!string.IsNullOrEmpty(request.PropertyKind))
        {
            query = query.Where(r => r.PropertyKind == request.PropertyKind);
        }

        var rates = await query
            .OrderBy(r => r.DisplaySeq)
            .Select(r => new FireInsuranceRateDto(r.Code, r.Condition, r.PropertyKind, r.RatePerSqm, r.DisplaySeq))
            .ToListAsync(cancellationToken);

        return new GetFireInsuranceRatesResult(rates);
    }
}
