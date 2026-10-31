namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>
    /// Deletes the row in the database straight away, without SaveChanges: used after a failed workflow resume to
    /// remove the email that attempt already committed.
    /// </summary>
    Task DeleteNowAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The admin's choice for this assignment's handover: the newest Pending or NotSent row (tracked), null when the
    /// assign recorded none. Sent / Failed rows are earlier handovers' history.
    /// </summary>
    Task<AssignmentEmail?> GetLatestUnsentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
