namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateTemplate;

public record UpdateTemplateResult(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
