using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Resolves the appraisal chain a user-picked prior appraisal belongs to, entirely from
/// <c>appraisal.Appraisals.PrevAppraisalId</c> — no Collateral module involvement.
///
/// The picked appraisal only <em>locates the chain</em>. The handler walks UP to the chain's root,
/// then DOWN over the whole tree, so a user who picks the original appraisal when later inspections
/// already exist still resolves to the latest one. That sideways reach is exactly what the previous
/// CollateralMaster-based lookup provided, and it is why walking ancestors alone is not enough.
///
/// Replaces <c>GetMostRecentEngagementByPriorAppraisalQuery</c> and
/// <c>GetProgressiveInspectionCountByPriorAppraisalQuery</c>. Sourcing from the appraisal schema is
/// not merely equivalent — the engagement row was only ever a copy of these values, written after
/// completion, so reading the origin also removes the materialisation race that could stamp an
/// inspection number one too low, permanently.
///
/// Returns null when the picked appraisal does not exist or is soft-deleted.
/// </summary>
public record ResolveLatestInAppraisalChainQuery(Guid PickedPrevAppraisalId)
    : IRequest<AppraisalChainRef?>;

/// <param name="AppraisalId">
/// The most recently completed appraisal in the chain — the copy, company and fee source. Falls
/// back to the picked appraisal when nothing in the chain is completed.
/// </param>
/// <param name="CompanyId">
/// The external company that performed <paramref name="AppraisalId"/>, from its latest live
/// assignment. Null for internally-appraised work — there is no company to force or exclude then.
/// </param>
/// <param name="ProgressiveCount">
/// How many Construction-Inspection (Progressive) appraisals the chain already holds, excluding
/// cancelled ones. Callers derive the next inspection round as <c>ProgressiveCount + 1</c>.
/// </param>
public record AppraisalChainRef(
    Guid AppraisalId,
    Guid? CompanyId,
    string CompanyName,
    int ProgressiveCount);
