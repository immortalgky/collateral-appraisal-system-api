namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public record UpdateComparativeAnalysisTemplateRequest(
    string TemplateName,
    string? Description,
    bool? IsActive
);
