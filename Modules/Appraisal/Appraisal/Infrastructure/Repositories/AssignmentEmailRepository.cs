namespace Appraisal.Infrastructure.Repositories;

public class AssignmentEmailRepository(AppraisalDbContext dbContext)
    : BaseRepository<AssignmentEmail, Guid>(dbContext), IAssignmentEmailRepository
{
    public Task<AssignmentEmail?> GetDraftAsync(Guid appraisalId, CancellationToken cancellationToken = default) =>
        dbContext.AssignmentEmails
            .FirstOrDefaultAsync(e => e.AppraisalId == appraisalId && e.Status == AssignmentEmailStatus.Draft,
                cancellationToken);
}
