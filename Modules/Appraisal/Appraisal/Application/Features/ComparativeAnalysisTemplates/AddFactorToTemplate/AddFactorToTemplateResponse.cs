namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public record AddFactorToTemplateResponse(
    Guid TemplateFactorId,
    Guid TemplateId,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory,
    decimal? DefaultWeight,
    decimal? DefaultIntensity,
    bool IsCalculationFactor
);
