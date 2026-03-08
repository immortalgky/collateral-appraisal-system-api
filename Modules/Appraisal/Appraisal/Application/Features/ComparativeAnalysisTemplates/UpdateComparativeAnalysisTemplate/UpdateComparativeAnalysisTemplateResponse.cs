namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public record UpdateComparativeAnalysisTemplateResponse(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
