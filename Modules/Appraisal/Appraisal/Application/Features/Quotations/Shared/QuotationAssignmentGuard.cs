using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Refuses to quote an appraisal whose current assignment is not Pending. A case routed back after assignment keeps a
/// non-Pending row (the Administration page is locked for it), so quoting it would let the admin re-dispatch it.
/// Judges the same row AssignAppraisalCommandHandler does: the newest live one per <see cref="AppraisalAssignmentElection"/>.
/// </summary>
public class QuotationAssignmentGuard(AppraisalDbContext dbContext)
{
    /// <summary>An appraisal with no live assignment row is not refused.</summary>
    public async Task EnsureAwaitingAssignmentAsync(IEnumerable<Guid> appraisalIds, CancellationToken cancellationToken)
    {
        var ids = appraisalIds.Distinct().ToList();

        // Newest first by the election's own ordering, projected to what the check needs; the first row seen per
        // appraisal is its current one.
        var rows = await dbContext.AppraisalAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.AppraisalId))
            .NewestFirst()
            .Select(a => new { a.AppraisalId, a.AssignmentStatus })
            .ToListAsync(cancellationToken);

        var notPending = rows.GroupBy(r => r.AppraisalId).Select(g => g.First())
            .FirstOrDefault(r => r.AssignmentStatus != AssignmentStatus.Pending);
        if (notPending is not null)
            throw new BadRequestException(
                $"Appraisal '{notPending.AppraisalId}' is not awaiting assignment " +
                $"(assignment status '{notPending.AssignmentStatus.Code}'). " +
                "A routed-back case is sent on from the Decision Summary page.");
    }
}
