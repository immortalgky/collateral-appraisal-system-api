using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeletePricingAnalysisReference;

/// <summary>
/// Deletes a market-reference PricingAnalysis by its id (used by the reference list / group
/// References section). Guarded to reference subtypes only — property-group / project-model
/// valuation analyses are managed via their own anchors, not this endpoint.
/// </summary>
public record DeletePricingAnalysisReferenceCommand(
    Guid PricingAnalysisId
) : ICommand<DeletePricingAnalysisReferenceResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
