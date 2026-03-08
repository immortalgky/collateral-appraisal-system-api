namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetComparativeAnalysisTemplateById;

public record GetComparativeAnalysisTemplateByIdResult(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    IReadOnlyList<TemplateFactorDto> ComparativeFactors,
    IReadOnlyList<TemplateFactorDto> CalculationFactors
);

public record TemplateFactorDto(
    Guid Id,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory,
    decimal? DefaultWeight,
    decimal? DefaultIntensity,
    bool IsCalculationFactor
);
