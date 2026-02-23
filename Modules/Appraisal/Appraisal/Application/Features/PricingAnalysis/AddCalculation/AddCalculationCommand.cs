using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddCalculation;

public record AddCalculationCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    Guid MarketComparableId
) : ICommand<AddCalculationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
