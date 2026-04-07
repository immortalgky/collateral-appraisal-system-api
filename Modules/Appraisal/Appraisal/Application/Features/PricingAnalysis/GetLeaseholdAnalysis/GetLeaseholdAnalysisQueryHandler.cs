using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetLeaseholdAnalysis;

public class GetLeaseholdAnalysisQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetLeaseholdAnalysisQuery, GetLeaseholdAnalysisResult>
{
    public async Task<GetLeaseholdAnalysisResult> Handle(
        GetLeaseholdAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(query.PricingAnalysisId, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"Pricing analysis {query.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId)
            ?? throw new InvalidOperationException($"Method {query.MethodId} not found");

        if (method.LeaseholdAnalysis is null)
            return new GetLeaseholdAnalysisResult(null, method.Remark);

        var la = method.LeaseholdAnalysis;
        var dto = new LeaseholdAnalysisDto(
            la.Id,
            la.PricingMethodId,
            la.LandValuePerSqWa,
            la.LandGrowthRateType,
            la.LandGrowthRatePercent,
            la.LandGrowthIntervalYears,
            la.ConstructionCostIndex,
            la.InitialBuildingValue,
            la.DepreciationRate,
            la.DepreciationIntervalYears,
            la.BuildingCalcStartYear,
            la.DiscountRate,
            la.TotalIncomeOverLeaseTerm,
            la.ValueAtLeaseExpiry,
            la.FinalValue,
            la.FinalValueRounded,
            la.IsPartialUsage,
            la.PartialRai,
            la.PartialNgan,
            la.PartialWa,
            la.PartialLandArea,
            la.PricePerSqWa,
            la.PartialLandPrice,
            la.EstimateNetPrice,
            la.EstimatePriceRounded,
            la.LandGrowthPeriods.OrderBy(p => p.FromYear).Select(p => new LandGrowthPeriodDto(
                p.Id, p.FromYear, p.ToYear, p.GrowthRatePercent
            )).ToList(),
            la.TableRows.OrderBy(r => r.DisplaySequence).Select(r => new LeaseholdCalculationDetailDto(
                r.Year, r.LandValue, r.LandGrowthPercent,
                r.BuildingValue, r.DepreciationAmount, r.DepreciationPercent,
                r.BuildingAfterDepreciation, r.TotalLandAndBuilding,
                r.RentalIncome, r.PvFactor, r.NetCurrentRentalIncome
            )).ToList()
        );

        return new GetLeaseholdAnalysisResult(dto, method.Remark);
    }
}
