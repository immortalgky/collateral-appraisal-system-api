using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisAnalysis;

public record DeleteHypothesisAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
