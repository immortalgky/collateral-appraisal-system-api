namespace Appraisal.Infrastructure.Repositories;

public class AssignmentEmailRepository(AppraisalDbContext dbContext)
    : BaseRepository<AssignmentEmail, Guid>(dbContext), IAssignmentEmailRepository
{
    public Task DeleteNowAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails.Where(e => e.Id == id).ExecuteDeleteAsync(cancellationToken);
}
