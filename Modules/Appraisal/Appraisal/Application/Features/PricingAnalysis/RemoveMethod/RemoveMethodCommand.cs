using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.RemoveMethod;

public record RemoveMethodCommand(
    Guid PricingAnalysisId,
    Guid ApproachId,
    Guid MethodId
) : ICommand<RemoveMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
