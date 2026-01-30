namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

public record UpdateFactorScoreRequest(
    string? SubjectValue = null,
    decimal? SubjectScore = null,
    string? ComparableValue = null,
    decimal? ComparableScore = null,
    decimal? FactorWeight = null,
    decimal? AdjustmentPct = null,
    string? Remarks = null
);
