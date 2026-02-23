using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

/// <summary>
/// Command to add a new approach to a pricing analysis
/// </summary>
public record AddApproachCommand(
    Guid PricingAnalysisId,
    string ApproachType,
    decimal? Weight = null
) : ICommand<AddApproachResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
