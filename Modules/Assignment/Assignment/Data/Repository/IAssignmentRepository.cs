namespace Assignment.Data.Repository;

public interface IAssignmentRepository
{
    Task<Assignments.Models.Assignment> GetAssignmentById(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<Assignments.Models.Assignment> CreateAssignment(Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAssignment(long Id,Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}