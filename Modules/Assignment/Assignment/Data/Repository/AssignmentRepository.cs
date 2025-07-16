using System.Globalization;
using Assignment.Assignments.Exceptions;

namespace Assignment.Data.Repository;

public class AssignmentRepository(AssignmentDbContext dbContext) : IAssignmentRepository
{
    public async Task<Assignments.Models.Assignment> GetAssignment(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Assignments
            .Where(r => r.Id == requestId);

        if (asNoTracking) query = query.AsNoTracking();

        var request = await query.SingleOrDefaultAsync(cancellationToken);

        return request ?? throw new AssignmentNotFoundException(requestId);
    }

    public async Task<Assignments.Models.Assignment> CreateAssignment(Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default)
    {
        dbContext.Assignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return assignment;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}