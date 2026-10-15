using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

/// <summary>
/// Command applying a complete pricing selection — the primary method per approach plus the
/// final approach — in ONE transaction, raising the final-value event once.
/// <para>
/// Replaces the summary screen's old N+1 call sequence (one SelectMethod per changed approach,
/// then SelectApproach). Those endpoints remain registered for compatibility.
/// </para>
/// </summary>
/// <param name="FullyDescribedApproachIds">
/// Approaches whose selection <paramref name="Selections"/> states in full — their methods are
/// cleared before it is applied, so an omitted method becomes deselected. Approaches absent from
/// this list are left alone. Empty (the default) reproduces the original additive behaviour.
/// </param>
public record ApplySelectionCommand(
    Guid PricingAnalysisId,
    IReadOnlyCollection<ApproachMethodSelectionDto> Selections,
    Guid FinalApproachId,
    IReadOnlyCollection<Guid>? FullyDescribedApproachIds = null
) : ICommand<ApplySelectionResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

/// <summary>One approach's primary-method choice.</summary>
public record ApproachMethodSelectionDto(Guid ApproachId, Guid MethodId);
