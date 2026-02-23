namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisByGroup;

/// <summary>
/// Handler for getting pricing analysis by property group ID
/// </summary>
public class GetPricingAnalysisByGroupQueryHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : IQueryHandler<GetPricingAnalysisByGroupQuery, GetPricingAnalysisByGroupResult>
{
    public async Task<GetPricingAnalysisByGroupResult> Handle(
        GetPricingAnalysisByGroupQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByPropertyGroupIdAsync(
            query.PropertyGroupId,
            cancellationToken);

        if (pricingAnalysis is null)
            return new GetPricingAnalysisByGroupResult(null, null, null, null);

        return new GetPricingAnalysisByGroupResult(
            pricingAnalysis.Id,
            pricingAnalysis.PropertyGroupId,
            pricingAnalysis.Status,
            pricingAnalysis.FinalAppraisedValue
        );
    }
}