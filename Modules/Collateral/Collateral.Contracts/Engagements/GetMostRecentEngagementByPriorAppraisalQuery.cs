using MediatR;

namespace Collateral.Contracts.Engagements;

/// <summary>
/// Resolves the most-recent prior engagement for the collateral that a prior appraisal touched.
///
/// Used by the workflow consumer for both Appeal and Construction Inspection: the supplied
/// <paramref name="PrevAppraisalId"/> is only used to <em>locate the collateral</em> (via its
/// engagement's CollateralMaster). The result is the most-recent engagement on that master —
/// which may be a newer appraisal than the one supplied (e.g. a 2nd CI resolves to the 1st CI,
/// not the original). Appeal uses the company to EXCLUDE it; CI uses the company to FORCE it and
/// the AppraisalId as the copy + fee source.
///
/// Returns null when the prior appraisal has no engagement, or when the matched master has no
/// engagement carrying a company.
/// </summary>
public record GetMostRecentEngagementByPriorAppraisalQuery(Guid PrevAppraisalId)
    : IRequest<EngagementRef?>;

/// <summary>
/// The resolved most-recent engagement: the appraisal to copy/chain from and the company that
/// performed it.
/// </summary>
public record EngagementRef(Guid AppraisalId, Guid CompanyId, string CompanyName);
