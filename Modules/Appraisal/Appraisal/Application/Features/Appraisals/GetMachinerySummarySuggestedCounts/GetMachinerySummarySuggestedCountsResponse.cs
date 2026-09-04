namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Response carrying the derived machinery head-counts.
/// </summary>
public record GetMachinerySummarySuggestedCountsResponse(
    int SurveyedNumber,
    int AppraisalNumber,
    int InstalledAndUseCount,
    int AppraisalScrapCount,
    int AppraisedByDocumentCount,
    int NotInstalledCount
);
