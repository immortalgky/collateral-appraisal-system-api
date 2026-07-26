namespace Appraisal.Application.Features.PricingAnalysis.ApplySelection;

public record ApplySelectionResult(
    Guid FinalApproachId,
    string FinalApproachType,
    decimal? FinalAppraisedValue
);
