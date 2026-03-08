namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateComparativeAnalysisTemplate;

public record CreateComparativeAnalysisTemplateRequest(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description = null
);
