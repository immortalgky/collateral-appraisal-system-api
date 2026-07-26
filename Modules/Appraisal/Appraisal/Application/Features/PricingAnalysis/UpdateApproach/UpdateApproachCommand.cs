using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

/// <summary>
/// Command to update an existing approach's weight. Approach VALUES are derived from the selected
/// method and cannot be set through this command — see <see cref="UpdateApproachRequest"/>.
/// </summary>
public record UpdateApproachCommand(
    Guid PricingAnalysisId,
    Guid ApproachId,
    decimal? Weight = null
) : ICommand<UpdateApproachResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
