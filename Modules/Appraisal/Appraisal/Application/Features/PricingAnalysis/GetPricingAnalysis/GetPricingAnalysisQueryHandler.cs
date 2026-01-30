namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Handler for getting a pricing analysis by ID
/// </summary>
public class GetPricingAnalysisQueryHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : IQueryHandler<GetPricingAnalysisQuery, GetPricingAnalysisResult>
{
    public async Task<GetPricingAnalysisResult> Handle(
        GetPricingAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                  query.Id,
                                  cancellationToken)
                              ?? throw new InvalidOperationException($"Pricing analysis {query.Id} not found");

        var approaches = pricingAnalysis.Approaches.Select(a => new ApproachDto(
            a.Id,
            a.ApproachType
        )).ToList();

        return new GetPricingAnalysisResult(
            pricingAnalysis.Id,
            pricingAnalysis.PropertyGroupId,
            pricingAnalysis.Status,
            pricingAnalysis.FinalAppraisedValue,
            approaches
        );
    }
}

public record ApproachDto(
    Guid Id,
    string ApproachType
);