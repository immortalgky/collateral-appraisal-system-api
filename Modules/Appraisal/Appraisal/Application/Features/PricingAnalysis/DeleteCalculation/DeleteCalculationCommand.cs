using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteCalculation;

public record DeleteCalculationCommand(
    Guid PricingAnalysisId,
    Guid CalculationId
) : ICommand<DeleteCalculationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
