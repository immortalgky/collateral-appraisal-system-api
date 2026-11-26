namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensionLogs;

public class GetAppraisalSuspensionLogsQueryHandler(AppraisalDbContext dbContext)
    : IQueryHandler<GetAppraisalSuspensionLogsQuery, IReadOnlyList<AppraisalSuspensionLogDto>>
{
    public async Task<IReadOnlyList<AppraisalSuspensionLogDto>> Handle(
        GetAppraisalSuspensionLogsQuery query, CancellationToken cancellationToken)
    {
        return await dbContext.AppraisalSuspensionLogs
            .AsNoTracking()
            .Where(l => l.AppraisalId == query.AppraisalId)
            .OrderByDescending(l => l.ActionAt)
            .ThenByDescending(l => l.Id)
            .Select(l => new AppraisalSuspensionLogDto(l.Action, l.Reason, l.ActionAt, l.ActionBy))
            .ToListAsync(cancellationToken);
    }
}
