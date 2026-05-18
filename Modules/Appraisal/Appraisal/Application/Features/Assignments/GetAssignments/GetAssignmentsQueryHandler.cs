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
            a.CreatedAt,
            a.Cycles.OrderBy(c => c.CycleNumber).Select(c => new EngagementCycleDto(
                c.Id,
                c.CycleNumber,
                c.OpenedAt,
                c.ClosedAt,
                c.BusinessMinutes,
                c.Status)).ToList(),
            a.TotalExternalBusinessMinutes,
            a.SubmissionCount
        )).ToList();

        return new GetAssignmentsResult(dtos);
    }
}
