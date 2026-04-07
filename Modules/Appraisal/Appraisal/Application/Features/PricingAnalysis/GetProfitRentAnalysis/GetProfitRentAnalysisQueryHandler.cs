using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetProfitRentAnalysis;

public class GetProfitRentAnalysisQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetProfitRentAnalysisQuery, GetProfitRentAnalysisResult>
{
    public async Task<GetProfitRentAnalysisResult> Handle(
        GetProfitRentAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(query.PricingAnalysisId, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"Pricing analysis {query.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId)
            ?? throw new InvalidOperationException($"Method {query.MethodId} not found");

        if (method.ProfitRentAnalysis is null)
            return new GetProfitRentAnalysisResult(null, method.Remark);

        var pr = method.ProfitRentAnalysis;
        var dto = new ProfitRentAnalysisDto(
            pr.Id,
            pr.PricingMethodId,
            pr.MarketRentalFeePerSqWa,
            pr.GrowthRateType,
            pr.GrowthRatePercent,
            pr.GrowthIntervalYears,
            pr.DiscountRate,
            pr.IncludeBuildingCost,
            pr.TotalMarketRentalFee,
            pr.TotalContractRentalFee,
            pr.TotalReturnsFromLease,
            pr.TotalPresentValue,
            pr.FinalValueRounded,
            pr.EstimatePriceRounded,
            method.FinalValue?.BuildingCost,
            method.FinalValue?.AppraisalPriceWithBuilding,
            method.FinalValue?.AppraisalPriceWithBuildingRounded,
            pr.GrowthPeriods.OrderBy(p => p.FromYear).Select(p => new ProfitRentGrowthPeriodDto(
                p.Id, p.FromYear, p.ToYear, p.GrowthRatePercent
            )).ToList(),
            pr.TableRows.OrderBy(r => r.DisplaySequence).Select(r => new ProfitRentCalculationDetailDto(
                r.Year, r.NumberOfMonths, r.MarketRentalFeePerSqWa,
                r.MarketRentalFeeGrowthPercent, r.MarketRentalFeePerMonth,
                r.MarketRentalFeePerYear, r.ContractRentalFeePerYear,
                r.ReturnsFromLease, r.PvFactor, r.PresentValue
            )).ToList()
        );

        return new GetProfitRentAnalysisResult(dto, method.Remark);
    }
}
