namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplates;

public record GetTemplatesResult(IReadOnlyList<TemplateDto> Templates);

public record TemplateDto(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    int FactorCount
);
