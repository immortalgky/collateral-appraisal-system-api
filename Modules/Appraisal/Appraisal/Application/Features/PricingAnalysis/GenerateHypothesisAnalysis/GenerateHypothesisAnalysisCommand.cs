using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals.Hypothesis;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GenerateHypothesisAnalysis;

public record GenerateHypothesisAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    HypothesisVariant Variant
) : ICommand<GenerateHypothesisAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
