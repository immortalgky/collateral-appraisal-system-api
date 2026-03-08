using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetComparativeAnalysisTemplateById;

public class GetComparativeAnalysisTemplateByIdQueryHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : IQueryHandler<GetComparativeAnalysisTemplateByIdQuery, GetComparativeAnalysisTemplateByIdResult>
{
    public async Task<GetComparativeAnalysisTemplateByIdResult> Handle(
        GetComparativeAnalysisTemplateByIdQuery query,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdWithFactorsAsync(query.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {query.TemplateId} not found");

        var allFactors = template.Factors
            .OrderBy(f => f.DisplaySequence)
            .Select(f => new TemplateFactorDto(
                f.Id,
                f.FactorId,
                f.DisplaySequence,
                f.IsMandatory,
                f.DefaultWeight,
                f.DefaultIntensity,
                f.IsCalculationFactor
            ))
            .ToList();

        var calculationFactors = allFactors
            .Where(f => f.IsCalculationFactor)
            .ToList();

        return new GetComparativeAnalysisTemplateByIdResult(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.PropertyType,
            template.Description,
            template.IsActive,
            allFactors,
            calculationFactors
        );
    }
}
