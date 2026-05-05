using MediatR;

namespace Collateral.Contracts.AppealExclusion;

/// <summary>
/// Looks up the most recent prior appraisal company for a Land/Condo title.
/// Used by the workflow consumer to seed the `excludedCompanyId` variable
/// on workflow start, so a normal appraisal kickstart respects the appeal-exclusion rule.
///
/// Returns null when no master matches the title or the matching master has no engagements.
/// </summary>
public record GetMostRecentPriorCompanyQuery(
    string TitleNumber,
    string? TitleType,
    string? Province
) : IRequest<Guid?>;
