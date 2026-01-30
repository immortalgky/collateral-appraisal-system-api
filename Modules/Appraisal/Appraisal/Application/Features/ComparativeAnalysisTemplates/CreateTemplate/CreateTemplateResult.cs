namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public record CreateTemplateResult(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
