using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

/// <summary>
/// Command to update an existing approach
/// </summary>
public record UpdateApproachCommand(
    Guid PricingAnalysisId,
    Guid ApproachId,
    decimal? ApproachValue = null,
    decimal? Weight = null
) : ICommand<UpdateApproachResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
