namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateComparativeAnalysisTemplate;

public record CreateComparativeAnalysisTemplateResult(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
