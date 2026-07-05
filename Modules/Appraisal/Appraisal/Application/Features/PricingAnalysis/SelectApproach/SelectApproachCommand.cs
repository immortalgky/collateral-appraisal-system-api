using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.PricingAnalysis.SelectApproach;

/// <summary>
/// Command to select an approach as the final approach for this pricing analysis,
/// setting all other approaches as unselected. Propagates the approach's already-computed
/// ApproachValue up to FinalAppraisedValue.
/// </summary>
public record SelectApproachCommand(
    Guid PricingAnalysisId,
    Guid ApproachId
) : ICommand<SelectApproachResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
