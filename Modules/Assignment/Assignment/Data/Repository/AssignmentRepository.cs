using System.Globalization;
using Assignment.Assignments.Exceptions;

namespace Assignment.Data.Repository;

public class AssignmentRepository(AssignmentDbContext dbContext) : IAssignmentRepository
{
    public async Task<Assignments.Models.Assignment> GetAssignmentById(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Assignments
            .Where(r => r.RequestId == requestId);

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


    public async Task<bool> UpdateAssignment(long Id, Assignments.Models.Assignment assignment,
        CancellationToken cancellationToken = default)
    { 
        var request = await dbContext.Assignments.FindAsync(Id, cancellationToken);
        bool result = true;

        if (request is null)
            result = false;
        else
            request.UpdateDetail(
                assignment.RequestId,
                assignment.AssignmentMethod,
                assignment.ExternalCompanyId,
                assignment.ExternalCompanyAssignType,
                assignment.ExtApprStaff,
                assignment.ExtApprStaffAssignmentType,
                assignment.IntApprStaff,
                assignment.IntApprStaffAssignmentType,
                assignment.Remark
            );

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}