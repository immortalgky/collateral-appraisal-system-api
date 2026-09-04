namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Counts derived from <c>MachineryAppraisalDetails</c>. These are suggestions only — the stored
/// <c>MachineryAppraisalSummary</c> keeps whatever the appraiser typed and is never overwritten.
/// </summary>
public record GetMachinerySummarySuggestedCountsResult(
    int SurveyedNumber,
    int AppraisalNumber,
    int InstalledAndUseCount,
    int AppraisalScrapCount,
    int AppraisedByDocumentCount,
    int NotInstalledCount
);
