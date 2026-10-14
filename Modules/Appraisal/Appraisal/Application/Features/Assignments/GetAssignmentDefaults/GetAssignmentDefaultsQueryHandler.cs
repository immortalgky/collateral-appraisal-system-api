using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

public class GetAssignmentDefaultsQueryHandler(
    AppraisalDbContext dbContext,
    IAssignmentDefaultsProvider assignmentDefaultsProvider
) : IQueryHandler<GetAssignmentDefaultsQuery, GetAssignmentDefaultsResult>
{
    public async Task<GetAssignmentDefaultsResult> Handle(
        GetAssignmentDefaultsQuery query,
        CancellationToken cancellationToken)
    {
        // Projection only: query handlers must not load the aggregate just to read one field.
        var appraisalType = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == query.AppraisalId)
            .Select(a => a.AppraisalType)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var resolved = DefaultAssignmentResolver.Resolve(assignmentDefaultsProvider.Current, appraisalType);

        return new GetAssignmentDefaultsResult(
            resolved.AssignmentType,
            resolved.AssignmentMethod,
            resolved.InternalFollowupAssignmentMethod);
    }
}
