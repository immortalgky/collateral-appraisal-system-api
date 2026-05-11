using Collateral.Contracts.AppealExclusion;
using Collateral.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.Application.AppealExclusion;

/// <summary>
/// Resolves the most recent prior appraisal company for a Land/Condo collateral matched by title.
/// Per business rule, an appeal excludes only the most-recent appraiser — not full history.
///
/// Matching strategy: NULL inputs act as wildcards (same pattern as the lookup endpoint), so
/// request-time partial keys still match. If multiple masters match, picks the one with the
/// newest `LastAppraisedDate`. If nothing matches, returns null.
/// </summary>
public class GetMostRecentPriorCompanyQueryHandler(
    CollateralDbContext db
) : IRequestHandler<GetMostRecentPriorCompanyQuery, Guid?>
{
    public async Task<Guid?> Handle(GetMostRecentPriorCompanyQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.TitleNumber)) return null;

        // Land match — alias-aware: alias hits are resolved to their ParentMasterId so
        // the engagement lookup hits the IsMaster row that actually carries the engagements.
        var landMatches = await db.CollateralMasters
            .Where(m => !m.IsDeleted && m.CollateralType == "Land")
            .Where(m => m.LandDetail!.TitleNumber == query.TitleNumber
                && (query.TitleType == null || m.LandDetail.TitleType == query.TitleType)
                && (query.Province == null || m.LandDetail.Province == query.Province))
            .Select(m => m.IsMaster ? m.Id : m.ParentMasterId!.Value)
            .ToListAsync(ct);

        // Condo match — Condo is always IsMaster (singleton group) so no alias resolution needed.
        var condoMatches = await db.CollateralMasters
            .Where(m => !m.IsDeleted && m.CollateralType == "Condo" && m.IsMaster)
            .Where(m => m.CondoDetail!.TitleNumber == query.TitleNumber
                && (query.TitleType == null || m.CondoDetail.TitleType == query.TitleType))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var allMasterIds = landMatches.Concat(condoMatches).Distinct().ToList();
        if (allMasterIds.Count == 0) return null;

        return await db.CollateralEngagements
            .Where(e => allMasterIds.Contains(e.CollateralMasterId))
            .Where(e => e.AppraisalCompanyId != null)
            .OrderByDescending(e => e.AppraisalDate)
            .Select(e => e.AppraisalCompanyId)
            .FirstOrDefaultAsync(ct);
    }
}
