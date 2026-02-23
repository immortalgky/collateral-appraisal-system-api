namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public record CreateTemplateRequest(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description = null
);
