namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateFactorInTemplate;

public record UpdateFactorInTemplateRequest(
    bool IsMandatory = false,
    decimal? DefaultWeight = null,
    decimal? DefaultIntensity = null,
    bool IsCalculationFactor = false
);
