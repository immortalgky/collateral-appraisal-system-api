namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

public record AddFactorScoreRequest(
    Guid FactorId,
    decimal FactorWeight,
    string? SubjectValue = null,
    decimal? SubjectScore = null,
    string? ComparableValue = null,
    decimal? ComparableScore = null,
    decimal? AdjustmentPct = null,
    string? Remarks = null
);
