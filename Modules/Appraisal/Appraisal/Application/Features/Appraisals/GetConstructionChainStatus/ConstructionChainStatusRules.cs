using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Appraisals.GetConstructionChainStatus;

/// <summary>A completed appraisal in the tree. Cancelled and unfinished ones never get this far.</summary>
internal sealed record ChainAppraisal(
    Guid Id,
    string? AppraisalNumber,
    string AppraisalType,
    string? Purpose,
    int? InspectionNumber,
    DateTime CompletedAt);

/// <summary>
/// One appraisal's construction progress as <c>vw_AppraisalConstructionProgress</c> reports it:
/// <see cref="CurrentProgressPct"/> clamped and rounded for display, <see cref="IsUnderConstruction"/> read
/// off the unrounded figure (99.996% is not complete however it displays).
/// </summary>
internal sealed record ChainProgress(decimal CurrentProgressPct, bool IsUnderConstruction);

/// <summary>
/// The decision rules of <see cref="GetConstructionChainStatusQuery"/>, free of any data access.
///
/// <paramref name="completed"/> must be the tree's completed appraisals, newest completion first.
/// <paramref name="progress"/> holds the completed appraisals' figures plus the pick's own, if it has any.
/// </summary>
internal static class ConstructionChainStatusRules
{
    private static string StatusOf(ChainProgress p) =>
        p.IsUnderConstruction ? ConstructionChainStatuses.InProgress : ConstructionChainStatuses.Complete;

    public static ConstructionChainStatusResult Evaluate(
        Guid pickedId,
        IReadOnlyList<ChainAppraisal> completed,
        IReadOnlyDictionary<Guid, ChainProgress> progress,
        OpenInspectionRequestRef? openRequest)
    {
        // The most recently completed appraisal that holds any inspection — not just an ancestor of the
        // pick. A re-appraisal and the next inspection are siblings, so walking back up from the pick
        // would report the older round.
        var source = completed.FirstOrDefault(c => progress.ContainsKey(c.Id));

        var construction = source is null
            ? new ConstructionChainProgress(ConstructionChainStatuses.NoData, null, null, null, null, null)
            : new ConstructionChainProgress(
                StatusOf(progress[source.Id]),
                progress[source.Id].CurrentProgressPct,
                source.Id,
                source.AppraisalNumber,
                source.InspectionNumber,
                source.CompletedAt);

        // Purposes 06 and 11 are one group: both are AppraisalType Progressive. Sibling branches count.
        var picked = completed.FirstOrDefault(c => c.Id == pickedId);
        var later = picked is null
            ? null
            : completed.FirstOrDefault(c => c.Id != pickedId
                                            && c.AppraisalType == AppraisalTypes.Progressive
                                            && c.CompletedAt > picked.CompletedAt);
        var laterRef = later is null
            ? null
            : new LaterInspectionRef(later.Id, later.AppraisalNumber, later.InspectionNumber, later.CompletedAt);

        // Offered only when nothing blocks the pick already: anything after it that is not an inspection
        // (a re-appraisal, an appeal) lets the user carry on, with the newer book on offer.
        var latest = completed.Count > 0 ? completed[0] : null;
        var latestRef = laterRef is null && latest is not null && latest.Id != pickedId
            ? new LatestAppraisalRef(
                latest.Id, latest.AppraisalNumber, latest.Purpose, latest.CompletedAt,
                progress.TryGetValue(latest.Id, out var latestProgress) ? latestProgress.CurrentProgressPct : null)
            : null;

        // The pick's own figure only: the banner must not show a number that belongs to another appraisal.
        var selected = progress.TryGetValue(pickedId, out var own)
            ? new ConstructionSelectedProgress(StatusOf(own), own.CurrentProgressPct)
            : new ConstructionSelectedProgress(ConstructionChainStatuses.NoData, null);

        var blockReason =
            construction.Status == ConstructionChainStatuses.Complete ? ConstructionChainBlockReasons.ConstructionComplete
            : laterRef is not null ? ConstructionChainBlockReasons.LaterInspectionExists
            : openRequest is not null ? ConstructionChainBlockReasons.OpenInspectionRequest
            : null;

        return new ConstructionChainStatusResult(pickedId, construction, selected, laterRef, openRequest, latestRef, blockReason);
    }
}
