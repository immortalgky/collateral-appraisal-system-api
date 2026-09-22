namespace Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;

public class GetFireInsuranceRatesQueryHandler(
    AppraisalDbContext context
) : IQueryHandler<GetFireInsuranceRatesQuery, GetFireInsuranceRatesResult>
{
    public async Task<GetFireInsuranceRatesResult> Handle(
        GetFireInsuranceRatesQuery query,
        CancellationToken cancellationToken)
    {
        var rates = context.FireInsuranceRates.AsQueryable();

        if (!string.IsNullOrEmpty(query.PropertyKind))
        {
            rates = rates.Where(r => r.PropertyKind == query.PropertyKind);
        }

        var result = await rates
            .OrderBy(r => r.DisplaySeq)
            .Select(r => new FireInsuranceRateDto(
                r.Code, r.Condition, r.PropertyKind, r.RatePerSqm, r.DisplaySeq))
            .ToListAsync(cancellationToken);

        return new GetFireInsuranceRatesResult(result);
    }
}
