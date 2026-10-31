namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>
    /// Deletes the row in the database straight away, without SaveChanges: used after a failed workflow resume to
    /// remove the email that attempt already committed.
    /// </summary>
    Task DeleteNowAsync(Guid id, CancellationToken cancellationToken = default);
}
