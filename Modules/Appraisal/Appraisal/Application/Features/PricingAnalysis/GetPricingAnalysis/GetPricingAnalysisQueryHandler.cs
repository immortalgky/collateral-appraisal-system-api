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
            a.ApproachType,
            a.IsSelected,
            a.Methods.Select(m => new MethodDto(
                m.Id,
                m.MethodType,
                m.MethodValue,
                m.IsSelected,
                m.ComparativeAnalysisTemplateId
            )).ToList()
        )).ToList();

        return new GetPricingAnalysisResult(
            pricingAnalysis.Id,
            pricingAnalysis.PropertyGroupId,
            pricingAnalysis.Status,
            pricingAnalysis.FinalAppraisedValue,
            pricingAnalysis.UseSystemCalc,
            approaches
        );
    }
}

public record ApproachDto(
    Guid Id,
    string ApproachType,
    bool IsSelected,
    List<MethodDto> Methods
);

public record MethodDto(
    Guid Id,
    string MethodType,
    decimal? MethodValue,
    bool IsSelected,
    Guid? ComparativeAnalysisTemplateId
);