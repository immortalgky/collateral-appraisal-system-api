using Collateral.Contracts.Engagements;
using Collateral.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.Application.Engagements;

/// <summary>
/// Resolves the most-recent engagement for the collateral a prior appraisal touched.
///
/// Step 1: find the CollateralMaster(s) the prior appraisal's engagement is anchored on
/// (engagement is one-per-appraisal, anchored on the primary IsMaster). The master link already
/// encodes the full canonical dedup key — so this is an exact collateral match, not a fuzzy
/// title match rebuilt from sparse request data.
///
/// Step 2: return the most-recent (by AppraisalDate) engagement on those master(s) that carries
/// a company. That engagement's appraisal is the true source for copy/company/fee.
/// </summary>
public class GetMostRecentEngagementByPriorAppraisalQueryHandler(
    CollateralDbContext db
) : IRequestHandler<GetMostRecentEngagementByPriorAppraisalQuery, EngagementRef?>
{
    public async Task<EngagementRef?> Handle(
        GetMostRecentEngagementByPriorAppraisalQuery query,
        CancellationToken ct)
    {
        var masterIds = await db.CollateralEngagements
            .Where(e => e.AppraisalId == query.PrevAppraisalId)
            .Select(e => e.CollateralMasterId)
            .Distinct()
            .ToListAsync(ct);

        if (masterIds.Count == 0)
            return null;

        // The common case is a single master (engagement is one-per-appraisal, anchored on the
        // primary IsMaster). If the prior appraisal spanned multiple masters, we take the single
        // most-recent engagement across the union of those masters.
        // The `AppraisalCompanyId != null` filter is intentional: a company-less latest engagement
        // (an internal/bank-side appraisal) makes this return null, and the caller falls back to the
        // raw PrevAppraisalId — there's no external company to force/exclude in that case anyway.
        // Tie-break by CreatedAt (then Id, which is CreateVersion7 time-ordered) so same-day
        // AppraisalDates resolve deterministically — matching the sibling engagement handlers.
        var engagement = await db.CollateralEngagements
            .Where(e => masterIds.Contains(e.CollateralMasterId) && e.AppraisalCompanyId != null)
            .OrderByDescending(e => e.AppraisalDate)
            .ThenByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Select(e => new { e.AppraisalId, e.AppraisalCompanyId, e.AppraisalCompanyName })
            .FirstOrDefaultAsync(ct);

        if (engagement?.AppraisalCompanyId is null)
            return null;

        return new EngagementRef(
            engagement.AppraisalId,
            engagement.AppraisalCompanyId.Value,
            engagement.AppraisalCompanyName ?? string.Empty);
    }
}
