namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplateById;

public record GetTemplateByIdResponse(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    IReadOnlyList<TemplateFactorDto> Factors
);
