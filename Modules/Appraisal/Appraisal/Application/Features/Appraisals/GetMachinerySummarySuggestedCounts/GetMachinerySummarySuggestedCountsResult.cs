namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Counts derived from <c>MachineryAppraisalDetails</c>. These are suggestions only — the stored
/// <c>MachineryAppraisalSummary</c> keeps whatever the appraiser typed and is never overwritten.
/// </summary>
/// <param name="Groups">
/// The same six counts split by property group. The summary is appraisal-level while the machines
/// it counts are organised into groups, so the split is what lets an appraiser see where a total
/// came from — and which group to open when one does not look right.
/// </param>
public record GetMachinerySummarySuggestedCountsResult(
    int SurveyedNumber,
    int AppraisalNumber,
    int InstalledAndUseCount,
    int AppraisalScrapCount,
    int AppraisedByDocumentCount,
    int NotInstalledCount,
    IReadOnlyList<MachinerySuggestedCountsByGroup> Groups
);

/// <summary>One group's contribution to the appraisal-level counts.</summary>
/// <param name="GroupId">Null for machines that are not in any group yet.</param>
public record MachinerySuggestedCountsByGroup(
    Guid? GroupId,
    int? GroupNumber,
    string? GroupName,
    int SurveyedNumber,
    int AppraisalNumber,
    int InstalledAndUseCount,
    int AppraisalScrapCount,
    int AppraisedByDocumentCount,
    int NotInstalledCount
);
