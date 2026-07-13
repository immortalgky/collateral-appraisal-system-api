using Collateral.Contracts.Engagements;
using Collateral.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.Application.Engagements;

/// <summary>
/// Counts the Construction-Inspection (Progressive) engagements already recorded on the
/// collateral a prior appraisal touched. See
/// <see cref="GetProgressiveInspectionCountByPriorAppraisalQuery"/> for how the caller turns this
/// into the "next inspection number" (count + 1).
///
/// AppraisalType is denormalised onto the engagement row (collateral.CollateralEngagements), so the
/// count is a plain query on this DbContext — no cross-schema join to appraisal.Appraisals.
/// </summary>
public class GetProgressiveInspectionCountByPriorAppraisalQueryHandler(
    CollateralDbContext db
) : IRequestHandler<GetProgressiveInspectionCountByPriorAppraisalQuery, int>
{
    // Matches AppraisalTypes.Progressive in the Appraisal module (Collateral must not reference it).
    private const string ProgressiveAppraisalType = "Progressive";

    public async Task<int> Handle(
        GetProgressiveInspectionCountByPriorAppraisalQuery query,
        CancellationToken ct)
    {
        var masterIds = await db.CollateralEngagements
            .Where(e => e.AppraisalId == query.PrevAppraisalId)
            .Select(e => e.CollateralMasterId)
            .Distinct()
            .ToListAsync(ct);

        if (masterIds.Count == 0)
            return 0;

        return await db.CollateralEngagements
            .Where(e => masterIds.Contains(e.CollateralMasterId)
                        && e.AppraisalType == ProgressiveAppraisalType)
            .CountAsync(ct);
    }
}
