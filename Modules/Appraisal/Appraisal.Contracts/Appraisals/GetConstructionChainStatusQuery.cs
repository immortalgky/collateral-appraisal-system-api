using System.Text.Json.Serialization;
using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Where the construction work stands across the whole appraisal tree a user-picked prior appraisal belongs
/// to (up to the root, then down over every descendant, cancelled appraisals ignored), and whether a new
/// construction-inspection request (purpose 06 / 11) may copy the pick.
///
/// Read live — nothing is stored at tree level. The progress is the one the rest of the system reports
/// (<c>appraisal.vw_AppraisalConstructionProgress</c>), taken from the most recently completed appraisal in
/// the tree that holds any inspection.
///
/// Returns null when the picked appraisal does not exist or is soft-deleted.
/// </summary>
public record GetConstructionChainStatusQuery(Guid AppraisalId) : IRequest<ConstructionChainStatusResult?>;

/// <param name="Construction">Progress of the tree's most recently completed appraisal that has inspections.</param>
/// <param name="LaterInspection">
/// A Progressive appraisal in the tree completed after the picked one (sibling branches count); null if none.
/// </param>
/// <param name="OpenInspectionRequest">
/// A submitted, not yet completed or cancelled Progressive (06 / 11) request on the tree; null if none. Drafts
/// do not count.
/// </param>
/// <param name="LatestAppraisal">
/// The most recently completed appraisal in the tree when it is not the picked one and the pick is not already
/// blocked by <paramref name="LaterInspection"/>; null otherwise. Used to offer "use the latest book".
/// </param>
/// <param name="BlockReason">
/// Why a 06 / 11 request must not copy the pick, one of <see cref="ConstructionChainBlockReasons"/>; null when
/// it may. Precedence: ConstructionComplete, LaterInspectionExists, OpenInspectionRequest.
/// </param>
// The API drops null properties app-wide; this response is agreed with the front end as always carrying every
// key ("null when absent"), so the nullable ones are pinned to be written.
public record ConstructionChainStatusResult(
    Guid AppraisalId,
    ConstructionChainProgress Construction,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LaterInspectionRef? LaterInspection,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] OpenInspectionRequestRef? OpenInspectionRequest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] LatestAppraisalRef? LatestAppraisal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? BlockReason);

/// <param name="Status">One of <see cref="ConstructionChainStatuses"/>.</param>
/// <param name="ProgressPct">Reported progress, 0-100 with two decimals; null for NoData.</param>
public record ConstructionChainProgress(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? ProgressPct,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? SourceAppraisalId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? SourceAppraisalNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? SourceInspectionNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? SourceCompletedAt);

public record LaterInspectionRef(Guid AppraisalId, string? AppraisalNumber, int? InspectionNumber, DateTime CompletedAt);

public record OpenInspectionRequestRef(Guid RequestId, string? RequestNumber);

public record LatestAppraisalRef(Guid AppraisalId, string? AppraisalNumber, string? PurposeCode, DateTime CompletedAt);

public static class ConstructionChainStatuses
{
    public const string InProgress = "InProgress";
    public const string Complete = "Complete";
    public const string NoData = "NoData";
}

public static class ConstructionChainBlockReasons
{
    public const string ConstructionComplete = "ConstructionComplete";
    public const string LaterInspectionExists = "LaterInspectionExists";
    public const string OpenInspectionRequest = "OpenInspectionRequest";
}
