namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public record UpdateComparativeAnalysisTemplateResult(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive
);
