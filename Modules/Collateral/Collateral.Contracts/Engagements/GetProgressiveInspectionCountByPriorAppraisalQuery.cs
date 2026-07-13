using MediatR;

namespace Collateral.Contracts.Engagements;

/// <summary>
/// Counts how many Construction-Inspection (Progressive) appraisals have already been performed on
/// the collateral that a prior appraisal touched.
///
/// The supplied <paramref name="PrevAppraisalId"/> is only used to <em>locate the collateral</em>
/// (via its engagement's CollateralMaster) — exactly like
/// <see cref="GetMostRecentEngagementByPriorAppraisalQuery"/>. The count is over completed
/// engagements on that master whose <c>AppraisalType</c> is "Progressive".
///
/// Callers derive the <em>next</em> inspection number as <c>count + 1</c>: an original-only
/// collateral returns 0 (→ 1st inspection); once the 1st CI has completed it returns 1 (→ 2nd),
/// and so on. Engagement rows only exist for completed appraisals, so an in-flight inspection is
/// never counted against itself.
///
/// Returns 0 when the prior appraisal has no engagement / master.
/// </summary>
public record GetProgressiveInspectionCountByPriorAppraisalQuery(Guid PrevAppraisalId)
    : IRequest<int>;
