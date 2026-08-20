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
public record ApplySelectionCommand(
    Guid PricingAnalysisId,
    IReadOnlyCollection<ApproachMethodSelectionDto> Selections,
    Guid FinalApproachId
) : ICommand<ApplySelectionResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

/// <summary>One approach's primary-method choice.</summary>
public record ApproachMethodSelectionDto(Guid ApproachId, Guid MethodId);
