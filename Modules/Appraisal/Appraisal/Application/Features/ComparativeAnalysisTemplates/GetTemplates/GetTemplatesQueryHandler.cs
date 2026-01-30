using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplates;

public class GetTemplatesQueryHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : IQueryHandler<GetTemplatesQuery, GetTemplatesResult>
{
    public async Task<GetTemplatesResult> Handle(
        GetTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var templates = query.ActiveOnly
            ? await templateRepository.GetActiveTemplatesAsync(cancellationToken)
            : await templateRepository.GetAllAsync(cancellationToken);

        var dtos = templates.Select(t => new TemplateDto(
            t.Id,
            t.TemplateCode,
            t.TemplateName,
            t.PropertyType,
            t.Description,
            t.IsActive,
            t.Factors.Count
        )).ToList();

        return new GetTemplatesResult(dtos);
    }
}
