using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

/// <summary>
/// Idempotent command: find-or-create a reference PricingAnalysis for the given anchor coordinates.
/// On creation, a "Market" approach is pre-added.
/// </summary>
public record CreateOrGetReferenceCommand(
    PricingAnalysisSubjectType SubjectType,
    Guid AnchorId,
    string? AnchorRefKey,
    Guid? HostMethodId
) : ICommand<CreateOrGetReferenceResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
