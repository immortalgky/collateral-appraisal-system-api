namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Response carrying the derived machinery head-counts, in total and split by property group.
/// </summary>
/// <param name="Groups">
/// The same six counts split by property group. The summary is appraisal-level while the machines
/// it counts are organised into groups, so the split is what lets an appraiser see where a total
/// came from — and which group to open when one does not look right.
/// </param>
public record GetMachinerySummarySuggestedCountsResponse(
    int SurveyedNumber,
    int AppraisalNumber,
    int InstalledAndUseCount,
    int AppraisalScrapCount,
    int AppraisedByDocumentCount,
    int NotInstalledCount,
    IReadOnlyList<MachinerySuggestedCountsByGroupResponse> Groups
);

/// <summary>One group's contribution to the appraisal-level counts.</summary>
/// <param name="GroupId">Null for machines that are not in any group yet.</param>
public record MachinerySuggestedCountsByGroupResponse(
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
