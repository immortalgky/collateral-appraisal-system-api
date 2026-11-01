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
    /// assign recorded none. Queued / Sent / Failed rows were already handed over.
    /// </summary>
    Task<AssignmentEmail?> GetLatestUnsentAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queued → Failed in one conditional UPDATE (no read-modify-write, so a Sent confirmation that saved first is never
    /// overwritten by a stale failure report). False when the row is gone or no longer Queued.
    /// </summary>
    Task<bool> MarkFailedIfQueuedAsync(Guid id, string? reason, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queued (or Failed: a redrive of the request) → Sent in one conditional UPDATE. False when the row is gone or
    /// already Sent.
    /// </summary>
    Task<bool> MarkSentIfQueuedOrFailedAsync(Guid id, DateTime sentAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the assignment's Pending emails created at or before <paramref name="createdUpTo"/>: the choices of an attempt
    /// that never reached a handover (company selection found nobody). A newer Pending row is the next attempt's.
    /// </summary>
    Task DeletePendingCreatedUpToAsync(Guid assignmentId, DateTime createdUpTo, CancellationToken cancellationToken = default);

    /// <summary>The addresses each Queued or Sent email of this assignment went to (a Failed one never reached anybody).</summary>
    Task<List<List<string>>> GetHandedOverRecipientsAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
