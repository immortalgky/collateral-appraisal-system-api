using Microsoft.EntityFrameworkCore;

namespace Appraisal.Application.Features.Assignments.GetAssignments;

public class GetAssignmentsQueryHandler(AppraisalDbContext dbContext)
    : IQueryHandler<GetAssignmentsQuery, GetAssignmentsResult>
{
    public async Task<GetAssignmentsResult> Handle(
        GetAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.AppraisalId == query.AppraisalId)
            .Include(a => a.Cycles)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Filter after materialization: AssignmentStatus is a HasConversion value object and
        // EF cannot translate value-object equality to SQL. Row count per appraisal is tiny.
        var assignments = rows
            .Where(a => a.AssignmentStatus != AssignmentStatus.Rejected
                        && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .ToList();

        // Only an offline engagement stores a hand-keyed date; on every other path ValuationDate is
        // derived from the appointment and must not be presented as a keyed book date. Fetched once
        // and only when an offline row is present.
        DateTime? offlineBookDate = null;
        if (assignments.Any(a => a.IsOfflineEngagement))
        {
            offlineBookDate = await dbContext.ValuationAnalyses
                .AsNoTracking()
                .Where(v => v.AppraisalId == query.AppraisalId)
                .Select(v => (DateTime?)v.ValuationDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var dtos = assignments.Select(a => new AssignmentDto(
            a.Id,
            a.AppraisalId,
            a.AssignmentType.Code,
            a.AssignmentStatus.Code,
            a.AssigneeUserId,
            a.AssigneeCompanyId,
            a.InternalAppraiserId,
            a.InternalFollowupAssignmentMethod,
            a.AssignmentMethod,
            a.ReassignmentNumber,
            a.ProgressPercent,
            a.AssignedAt,
            a.AssignedBy,
            a.StartedAt,
            a.SubmittedAt,
            a.CompletedAt,
            a.RejectionReason,
            a.CancellationReason,
            a.Remark,
            a.DraftSavedAt,
            a.CreatedAt,
            a.Cycles.OrderBy(c => c.CycleNumber).Select(c => new EngagementCycleDto(
                c.Id,
                c.CycleNumber,
                c.OpenedAt,
                c.ClosedAt,
                c.BusinessMinutes,
                c.Status)).ToList(),
            a.TotalExternalBusinessMinutes,
            a.SubmissionCount,
            a.IsOfflineEngagement ? offlineBookDate : null
        )).ToList();

        return new GetAssignmentsResult(dtos);
    }
}
