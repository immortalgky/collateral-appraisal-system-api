namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public record CreateTemplateResponse(
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
