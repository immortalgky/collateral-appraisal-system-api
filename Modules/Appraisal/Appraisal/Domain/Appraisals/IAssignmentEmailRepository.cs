namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>The assignment's rows not sent yet (Pending, NotSent), tracked. Sent and Failed rows are history.</summary>
    Task<List<AssignmentEmail>> GetUnsentForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
