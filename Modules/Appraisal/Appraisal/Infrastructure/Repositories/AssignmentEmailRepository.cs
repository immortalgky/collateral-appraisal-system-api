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

    // ExecuteUpdate skips the audit interceptor: UpdatedAt (the failure time shown in the history) and UpdatedBy are set here.
    public async Task<bool> MarkFailedIfQueuedAsync(Guid id, string? reason, DateTime now, CancellationToken cancellationToken = default) =>
        await dbContext.AssignmentEmails.Where(e => e.Id == id && e.Status == AssignmentEmailStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, AssignmentEmailStatus.Failed)
                .SetProperty(e => e.FailureReason, AssignmentEmail.NormalizeFailureReason(reason))
                .SetProperty(e => e.UpdatedAt, now)
                .SetProperty(e => e.UpdatedBy, "anonymous"), cancellationToken) > 0;

    public async Task<bool> MarkSentIfQueuedOrFailedAsync(Guid id, DateTime sentAt, CancellationToken cancellationToken = default) =>
        await dbContext.AssignmentEmails
            .Where(e => e.Id == id && (e.Status == AssignmentEmailStatus.Queued || e.Status == AssignmentEmailStatus.Failed))
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, AssignmentEmailStatus.Sent)
                .SetProperty(e => e.SentAt, sentAt)
                .SetProperty(e => e.FailureReason, (string?)null)
                .SetProperty(e => e.UpdatedAt, sentAt)
                .SetProperty(e => e.UpdatedBy, "anonymous"), cancellationToken) > 0;

    public Task DeletePendingCreatedUpToAsync(Guid assignmentId, DateTime createdUpTo, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails
            .Where(e => e.AssignmentId == assignmentId && e.Status == AssignmentEmailStatus.Pending && e.CreatedAt <= createdUpTo)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<List<List<string>>> GetHandedOverRecipientsAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails.AsNoTracking()
            .Where(e => e.AssignmentId == assignmentId
                        && (e.Status == AssignmentEmailStatus.Queued || e.Status == AssignmentEmailStatus.Sent))
            .Select(e => e.SentTo)
            .ToListAsync(cancellationToken);
}
