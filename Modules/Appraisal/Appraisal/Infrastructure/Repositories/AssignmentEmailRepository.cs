namespace Appraisal.Infrastructure.Repositories;

public class AssignmentEmailRepository(AppraisalDbContext dbContext)
    : BaseRepository<AssignmentEmail, Guid>(dbContext), IAssignmentEmailRepository
{
    public Task<List<AssignmentEmail>> GetUnsentForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails
            .Where(e => e.AssignmentId == assignmentId
                        && (e.Status == AssignmentEmailStatus.Pending || e.Status == AssignmentEmailStatus.NotSent))
            .ToListAsync(cancellationToken);
}
