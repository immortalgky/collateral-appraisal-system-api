namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

/// <summary>Handler for getting project unit upload history.</summary>
public class GetProjectUnitUploadsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetProjectUnitUploadsQuery, GetProjectUnitUploadsResult>
{
    public async Task<GetProjectUnitUploadsResult> Handle(
        GetProjectUnitUploadsQuery query,
        CancellationToken cancellationToken)
    {
        // Resolve ProjectId from AppraisalId
        var projectId = await dbContext.Projects
            .Where(p => p.AppraisalId == query.AppraisalId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId is null)
            return new GetProjectUnitUploadsResult([]);

        var uploads = await dbContext.ProjectUnitUploads
            .Where(u => u.ProjectId == projectId.Value)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new ProjectUnitUploadDto(
                u.Id, u.ProjectId, u.FileName, u.UploadedAt, u.IsUsed, u.DocumentId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new GetProjectUnitUploadsResult(uploads);
    }
}
