using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetComparativeAnalysisTemplates;

public class GetComparativeAnalysisTemplatesQueryHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : IQueryHandler<GetComparativeAnalysisTemplatesQuery, GetComparativeAnalysisTemplatesResult>
{
    public async Task<GetComparativeAnalysisTemplatesResult> Handle(
        GetComparativeAnalysisTemplatesQuery query,
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

        return new GetComparativeAnalysisTemplatesResult(dtos);
    }
}
