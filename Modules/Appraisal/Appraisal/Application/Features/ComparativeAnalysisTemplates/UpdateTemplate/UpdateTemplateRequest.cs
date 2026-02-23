namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateTemplate;

public record UpdateTemplateRequest(
    string TemplateName,
    string? Description,
    bool? IsActive
);
