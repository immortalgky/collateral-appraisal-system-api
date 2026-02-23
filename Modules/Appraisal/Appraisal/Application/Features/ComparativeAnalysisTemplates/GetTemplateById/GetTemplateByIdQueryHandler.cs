using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplateById;

public class GetTemplateByIdQueryHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : IQueryHandler<GetTemplateByIdQuery, GetTemplateByIdResult>
{
    public async Task<GetTemplateByIdResult> Handle(
        GetTemplateByIdQuery query,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdWithFactorsAsync(query.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {query.TemplateId} not found");

        var factors = template.Factors
            .OrderBy(f => f.DisplaySequence)
            .Select(f => new TemplateFactorDto(
                f.Id,
                f.FactorId,
                f.DisplaySequence,
                f.IsMandatory,
                f.DefaultWeight
            ))
            .ToList();

        return new GetTemplateByIdResult(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.PropertyType,
            template.Description,
            template.IsActive,
            factors
        );
    }
}
