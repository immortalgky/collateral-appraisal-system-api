namespace Appraisal.Infrastructure.Repositories;

public class AssignmentEmailRepository(AppraisalDbContext dbContext)
    : BaseRepository<AssignmentEmail, Guid>(dbContext), IAssignmentEmailRepository
{
    public Task<List<AssignmentEmail>> GetForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails.Where(e => e.AssignmentId == assignmentId).ToListAsync(cancellationToken);
}
