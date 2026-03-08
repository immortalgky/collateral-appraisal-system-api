using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.ResetPricingMethod;

public record ResetPricingMethodCommand(
    Guid PricingAnalysisId,
    Guid MethodId
) : ICommand<ResetPricingMethodResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
