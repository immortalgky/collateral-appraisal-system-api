namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>The assignment's email rows, tracked.</summary>
    Task<List<AssignmentEmail>> GetForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
