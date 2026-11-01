namespace Appraisal.Infrastructure.Repositories;

public class AssignmentEmailRepository(AppraisalDbContext dbContext)
    : BaseRepository<AssignmentEmail, Guid>(dbContext), IAssignmentEmailRepository
{
    public Task DeleteNowAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails.Where(e => e.Id == id).ExecuteDeleteAsync(cancellationToken);

    public Task<AssignmentEmail?> GetLatestUnsentAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails
            .Where(e => e.AssignmentId == assignmentId
                        && (e.Status == AssignmentEmailStatus.Pending || e.Status == AssignmentEmailStatus.NotSent))
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<List<string>>> GetHandedOverRecipientsAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails.AsNoTracking()
            .Where(e => e.AssignmentId == assignmentId
                        && (e.Status == AssignmentEmailStatus.Queued || e.Status == AssignmentEmailStatus.Sent))
            .Select(e => e.SentTo)
            .ToListAsync(cancellationToken);
}
