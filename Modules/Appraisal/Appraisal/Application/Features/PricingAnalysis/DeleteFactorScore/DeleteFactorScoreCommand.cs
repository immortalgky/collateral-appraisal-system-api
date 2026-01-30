using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteFactorScore;

/// <summary>
/// Command to delete a factor score from a pricing calculation
/// </summary>
public record DeleteFactorScoreCommand(
    Guid PricingAnalysisId,
    Guid FactorScoreId
) : ICommand<DeleteFactorScoreResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
