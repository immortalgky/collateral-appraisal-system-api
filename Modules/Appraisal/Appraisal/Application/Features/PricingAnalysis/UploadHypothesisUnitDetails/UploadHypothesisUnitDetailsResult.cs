namespace Appraisal.Application.Features.PricingAnalysis.UploadHypothesisUnitDetails;

public record UploadHypothesisUnitDetailsResult(
    Guid UploadId,
    int RowCount,
    bool IsActive
);
