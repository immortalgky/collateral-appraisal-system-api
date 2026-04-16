using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetIncomeAnalysis;

public class GetIncomeAnalysisQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetIncomeAnalysisQuery, GetIncomeAnalysisResult>
{
    public async Task<GetIncomeAnalysisResult> Handle(
        GetIncomeAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
                                 query.PricingAnalysisId, cancellationToken)
                             ?? throw new NotFoundException(
                                 nameof(Domain.Appraisals.PricingAnalysis),
                                 query.PricingAnalysisId);

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId)
            ?? throw new NotFoundException(nameof(PricingAnalysisMethod), query.MethodId);

        if (method.IncomeAnalysis is null)
            return new GetIncomeAnalysisResult(null);

        return new GetIncomeAnalysisResult(
            IncomeAnalysisMapper.ToDto(method.IncomeAnalysis));
    }
}
