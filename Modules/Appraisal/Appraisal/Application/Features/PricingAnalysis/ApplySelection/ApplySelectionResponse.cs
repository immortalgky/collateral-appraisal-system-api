namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

public record ApplySelectionResponse(
    Guid FinalApproachId,
    string FinalApproachType,
    decimal? FinalAppraisedValue
);

/// <summary>
/// Request body for POST /pricing-analysis/{id}/selection.
/// <para>
/// <c>FullyDescribedApproachIds</c> lists the approaches this request describes in full — their
/// selections are cleared before <c>Selections</c> is applied, so an omitted method becomes
/// deselected. See ApplySelectionCommand. Optional: omitting it keeps the purely additive
/// behaviour older clients rely on.
/// </para>
/// </summary>
public record ApplySelectionRequest(
    IReadOnlyCollection<ApproachMethodSelectionDto> Selections,
    Guid FinalApproachId,
    IReadOnlyCollection<Guid>? FullyDescribedApproachIds = null
);
