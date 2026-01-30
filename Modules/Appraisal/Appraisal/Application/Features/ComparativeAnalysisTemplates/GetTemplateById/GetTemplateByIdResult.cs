namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.GetTemplateById;

public record GetTemplateByIdResult(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    IReadOnlyList<TemplateFactorDto> Factors
);

public record TemplateFactorDto(
    Guid Id,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory,
    decimal? DefaultWeight
);
