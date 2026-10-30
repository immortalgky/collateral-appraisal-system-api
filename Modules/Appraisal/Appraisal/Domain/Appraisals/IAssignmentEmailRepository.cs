namespace Appraisal.Domain.Appraisals;

public interface IAssignmentEmailRepository : IRepository<AssignmentEmail, Guid>
{
    /// <summary>The appraisal's one <see cref="AssignmentEmailStatus.Draft"/> email, tracked; null when none.</summary>
    Task<AssignmentEmail?> GetDraftAsync(Guid appraisalId, CancellationToken cancellationToken = default);
}
