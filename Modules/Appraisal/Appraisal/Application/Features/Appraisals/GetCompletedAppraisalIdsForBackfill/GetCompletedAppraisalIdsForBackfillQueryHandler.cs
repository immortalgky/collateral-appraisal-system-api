using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetCompletedAppraisalIdsForBackfill;

/// <summary>
/// Returns a page of completed appraisal Ids ordered oldest CompletedAt first.
/// Used by CollateralBackfillJob to stream appraisals without loading the full aggregate.
/// </summary>
public class GetCompletedAppraisalIdsForBackfillQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetCompletedAppraisalIdsForBackfillQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCompletedAppraisalIdsForBackfillQuery query,
        CancellationToken cancellationToken)
    {
        return await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Status.Code == "Completed")
            .OrderBy(a => a.CompletedAt)
            .ThenBy(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
    }
}
