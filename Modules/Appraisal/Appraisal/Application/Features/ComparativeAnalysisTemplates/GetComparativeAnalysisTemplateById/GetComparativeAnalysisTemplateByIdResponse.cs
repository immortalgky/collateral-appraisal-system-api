namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetComparativeAnalysisTemplateById;

public record GetComparativeAnalysisTemplateByIdResponse(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    IReadOnlyList<TemplateFactorDto> ComparativeFactors,
    IReadOnlyList<TemplateFactorDto> CalculationFactors
);
