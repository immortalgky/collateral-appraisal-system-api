namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public record AddFactorToTemplateResult(
    Guid TemplateFactorId,
    Guid TemplateId,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory,
    decimal? DefaultWeight
);
