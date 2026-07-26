namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

public record ApplySelectionResponse(
    Guid FinalApproachId,
    string FinalApproachType,
    decimal? FinalAppraisedValue
);

/// <summary>Request body for POST /pricing-analysis/{id}/selection.</summary>
public record ApplySelectionRequest(
    IReadOnlyCollection<ApproachMethodSelectionDto> Selections,
    Guid FinalApproachId
);
