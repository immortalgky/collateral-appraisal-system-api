using Collateral.Contracts.ConstructionInspection;
using Collateral.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Collateral.CollateralMasters.Application.ConstructionInspection;

/// <summary>
/// Resolves the most recent appraisal company for a given appraisal ID.
/// Used for Construction Inspection routing: the same company that appraised
/// the prior (base) appraisal must be re-engaged for the follow-up CI appraisal.
///
/// Queries CollateralEngagements for the given AppraisalId, ordered by AppraisalDate DESC,
/// and returns the first engagement that has a company set. Returns null if none found.
/// </summary>
public class GetMostRecentCompanyForAppraisalQueryHandler(
    CollateralDbContext db
) : IRequestHandler<GetMostRecentCompanyForAppraisalQuery, (Guid CompanyId, string CompanyName)?>
{
    public async Task<(Guid CompanyId, string CompanyName)?> Handle(
        GetMostRecentCompanyForAppraisalQuery query,
        CancellationToken ct)
    {
        var engagement = await db.CollateralEngagements
            .Where(e => e.AppraisalId == query.AppraisalId && e.AppraisalCompanyId != null)
            .OrderByDescending(e => e.AppraisalDate)
            .Select(e => new { e.AppraisalCompanyId, e.AppraisalCompanyName })
            .FirstOrDefaultAsync(ct);

        if (engagement?.AppraisalCompanyId is null)
            return null;

        var companyName = engagement.AppraisalCompanyName ?? string.Empty;
        return (engagement.AppraisalCompanyId.Value, companyName);
    }
}
