namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>
    /// Deletes the row in the database straight away, outside the unit of work: used after a failed workflow resume,
    /// when the context may still hold that attempt's other changes, which must not be saved.
    /// </summary>
    Task DeleteNowAsync(Guid id, CancellationToken cancellationToken = default);
}
