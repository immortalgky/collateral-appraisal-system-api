using Appraisal.Application.Configurations;
using Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CreateReferenceFromMethod;

/// <summary>
/// Creates a reference PricingAnalysis by deep-cloning an existing Cost-approach method
/// (WQS / SaleGrid / DirectComparison) into a new "Market" approach, optionally overriding
/// the land area. Idempotent: returns the existing reference if one already exists for the anchor.
/// </summary>
public record CreateReferenceFromMethodCommand(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    Guid? HostMethodId,
    Guid SourcePricingAnalysisId,
    Guid SourceMethodId,
    decimal? LandAreaOverride
) : ICommand<CreateOrGetReferenceResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
