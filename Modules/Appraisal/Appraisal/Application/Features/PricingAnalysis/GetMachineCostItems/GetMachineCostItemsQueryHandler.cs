using Appraisal.Domain.Appraisals;
using Dapper;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public class GetMachineCostItemsQueryHandler(
    IPricingAnalysisRepository repository,
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetMachineCostItemsQuery, GetMachineCostItemsResult>
{
    public async Task<GetMachineCostItemsResult> Handle(
        GetMachineCostItemsQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(query.PricingAnalysisId, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"Pricing analysis {query.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId)
            ?? throw new InvalidOperationException($"Method {query.MethodId} not found");

        var orderedItems = method.MachineCostItems
            .OrderBy(i => i.DisplaySequence)
            .ToList();

        var propertyIds = orderedItems
            .Select(i => i.AppraisalPropertyId)
            .Distinct()
            .ToArray();

        var nameByPropertyId = await LoadPropertyNamesAsync(propertyIds);

        var items = orderedItems
            .Select(i => new MachineCostItemDto(
                i.Id,
                i.AppraisalPropertyId,
                nameByPropertyId.GetValueOrDefault(i.AppraisalPropertyId),
                i.DisplaySequence,
                i.RcnReplacementCost,
                i.LifeSpanYears,
                i.ConditionFactor,
                i.FunctionalObsolescence,
                i.EconomicObsolescence,
                i.FairMarketValue,
                i.MarketDemandAvailable,
                i.Notes
            ))
            .ToList();

        var totalFmv = items.Sum(i => i.FairMarketValue ?? 0);

        return new GetMachineCostItemsResult(items, totalFmv, method.Remark);
    }

    private async Task<Dictionary<Guid, string?>> LoadPropertyNamesAsync(Guid[] propertyIds)
    {
        if (propertyIds.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        const string sql = @"
SELECT ap.Id AS AppraisalPropertyId,
       COALESCE(
           NULLIF(mac.PropertyName, ''),
           NULLIF(mac.MachineName, ''),
           NULLIF(veh.PropertyName, ''),
           NULLIF(veh.VehicleName, ''),
           NULLIF(ves.PropertyName, ''),
           NULLIF(ves.VesselName, ''),
           NULLIF(ap.Description, '')
       ) AS PropertyName
FROM appraisal.AppraisalProperties ap
LEFT JOIN appraisal.MachineryAppraisalDetails mac ON mac.AppraisalPropertyId = ap.Id
LEFT JOIN appraisal.VehicleAppraisalDetails    veh ON veh.AppraisalPropertyId = ap.Id
LEFT JOIN appraisal.VesselAppraisalDetails     ves ON ves.AppraisalPropertyId = ap.Id
WHERE ap.Id IN @PropertyIds";

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid AppraisalPropertyId, string? PropertyName)>(
            sql,
            new { PropertyIds = propertyIds });

        return rows.ToDictionary(r => r.AppraisalPropertyId, r => r.PropertyName);
    }
}
