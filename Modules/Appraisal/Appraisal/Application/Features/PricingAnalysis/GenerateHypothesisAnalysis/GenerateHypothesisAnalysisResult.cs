using Appraisal.Domain.Appraisals.Hypothesis;

namespace Appraisal.Application.Features.PricingAnalysis.GenerateHypothesisAnalysis;

public record GenerateHypothesisAnalysisResult(
    Guid HypothesisAnalysisId,
    Guid MethodId,
    HypothesisVariant Variant
);
