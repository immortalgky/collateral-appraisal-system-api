namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public record AddFactorToTemplateRequest(
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory = false,
    decimal? DefaultWeight = null
);
