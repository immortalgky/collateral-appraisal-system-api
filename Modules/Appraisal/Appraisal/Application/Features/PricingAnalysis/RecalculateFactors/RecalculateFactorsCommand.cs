using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

/// <summary>
/// Command to recalculate total factor adjustment for a pricing calculation
/// </summary>
public record RecalculateFactorsCommand(
    Guid PricingAnalysisId,
    Guid PricingCalculationId
) : ICommand<RecalculateFactorsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
