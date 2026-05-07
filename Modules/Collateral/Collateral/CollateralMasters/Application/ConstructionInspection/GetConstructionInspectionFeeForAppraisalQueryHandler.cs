using Collateral.Contracts.ConstructionInspection;
using Collateral.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.Application.ConstructionInspection;

/// <summary>
/// Resolves the most recent CI fee for a given prior appraisal ID. Used to seed a new
/// Construction Inspection appraisal's fee from the prior engagement.
///
/// Picks the most recent (by AppraisalDate) engagement that carries a non-null
/// ConstructionInspectionFeeAmount. Returns null when none exists — caller should
/// leave the fee blank in that case (no fallback to generic appraisal fee per spec).
/// </summary>
public class GetConstructionInspectionFeeForAppraisalQueryHandler(
    CollateralDbContext db
) : IRequestHandler<GetConstructionInspectionFeeForAppraisalQuery, decimal?>
{
    public async Task<decimal?> Handle(
        GetConstructionInspectionFeeForAppraisalQuery query,
        CancellationToken ct)
    {
        return await db.CollateralEngagements
            .Where(e => e.AppraisalId == query.AppraisalId
                        && e.ConstructionInspectionFeeAmount != null)
            .OrderByDescending(e => e.AppraisalDate)
            .Select(e => e.ConstructionInspectionFeeAmount)
            .FirstOrDefaultAsync(ct);
    }
}
