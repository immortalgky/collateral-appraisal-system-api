using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.Identity;

namespace Appraisal.Application.Features.Shared;

/// <summary>
/// Decides which fields of an appraisal a caller is allowed to receive.
///
/// Sits beside <see cref="AppraisalAccessScope"/>, which answers the other half of the
/// question — that one filters WHICH ROWS come back, this one filters WHICH COLUMNS.
///
/// Credit officers hold <c>APPRAISAL_TRACKING_VIEW</c> and not <c>APPRAISAL_VIEW</c>. They
/// are allowed to see every appraisal (a deliberate product decision — there is no row
/// predicate for them), so masking is the only control left, and it therefore has to happen
/// server-side: the list DTO already carries <c>AppraisalValue</c> on every row, and hiding a
/// column in the client leaves the number sitting in the JSON for anyone who opens DevTools.
/// </summary>
public static class AppraisalFieldScope
{
    private const string FullView = "APPRAISAL_VIEW";
    private const string TrackingView = "APPRAISAL_TRACKING_VIEW";

    /// <summary>Status that releases the appraised value. See <c>Appraisal.MarkApprovedByCommittee</c>.</summary>
    private const string ApprovedStatus = "Completed";

    /// <summary>
    /// True when the caller reached this endpoint on the tracking permission alone. Holding
    /// <c>APPRAISAL_VIEW</c> always wins, so an internal user who happens to also be granted
    /// the tracking permission is never degraded.
    /// </summary>
    public static bool IsTrackingOnly(ICurrentUserService user) =>
        !user.HasPermission(FullView) && user.HasPermission(TrackingView);

    /// <summary>
    /// True when the caller can open the appraisal workspace at all.
    ///
    /// Use THIS — not <see cref="IsTrackingOnly"/> — to decide where to send someone. The two are
    /// not complements: a caller holding NEITHER permission (dev-bypass, or a role that reaches
    /// the search box without either code) is not "tracking only", so keying a destination off
    /// IsTrackingOnly sends them to /appraisals/{id}, which the route guard then bounces to '/'.
    /// The question a link has to answer is "can they open it", and this is that question.
    ///
    /// Masking is the other way round on purpose: it is a disclosure control, so it fires only for
    /// someone positively identified as credit, never merely from a permission being absent.
    /// </summary>
    public static bool CanOpenWorkspace(ICurrentUserService user) => user.HasPermission(FullView);

    /// <summary>
    /// True when this appraisal's value and documents may be released. Cancelled work never
    /// qualifies: it is closed without a price, so there is nothing to release.
    /// </summary>
    public static bool IsReleased(string? status) =>
        string.Equals(status, ApprovedStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Withholds the appraised value until the committee has approved it.
    ///
    /// That is now the ONLY thing masked. The assignment and appraiser columns used to clear too,
    /// on the reasoning that who is doing the work is internal — but the tracking panel this was
    /// built for shows the current holder, the valuation firm and its contact details by design,
    /// so the list was hiding what the panel beside it published. One of the two had to go, and
    /// the business chose to keep the panel (2026-09-15).
    ///
    /// The value is not secret either, it is merely <i>not final</i> — hence a release rule rather
    /// than a blanket. Cancelled work never qualifies: it closed without a price.
    /// </summary>
    public static AppraisalDto Mask(AppraisalDto dto) => dto with
    {
        AppraisalValue = IsReleased(dto.Status) ? dto.AppraisalValue : null
    };

    /// <summary>
    /// Same rule for the single-appraisal read. This DTO uses mutable properties rather than
    /// <c>init</c>, so it is mutated in place instead of copied with <c>with</c>.
    /// </summary>
    public static GetAppraisalByIdResult Mask(GetAppraisalByIdResult dto)
    {
        if (!IsReleased(dto.Status)) dto.AppraisalValue = null;
        return dto;
    }

    /// <summary>Convenience for the list paths: masks every row, or none, in one call.</summary>
    public static IEnumerable<AppraisalDto> MaskAll(IEnumerable<AppraisalDto> rows, ICurrentUserService user) =>
        IsTrackingOnly(user) ? rows.Select(Mask) : rows;
}
