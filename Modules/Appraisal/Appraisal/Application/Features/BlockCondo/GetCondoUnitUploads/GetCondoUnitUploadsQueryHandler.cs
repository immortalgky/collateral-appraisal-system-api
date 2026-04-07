namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitUploads;

public class GetCondoUnitUploadsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetCondoUnitUploadsQuery, GetCondoUnitUploadsResult>
{
    public async Task<GetCondoUnitUploadsResult> Handle(
        GetCondoUnitUploadsQuery query,
        CancellationToken cancellationToken)
    {
        var uploads = await dbContext.CondoUnitUploads
            .Where(u => u.AppraisalId == query.AppraisalId)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new CondoUnitUploadDto(
                u.Id, u.AppraisalId, u.FileName,
                u.UploadedAt, u.IsUsed, u.DocumentId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new GetCondoUnitUploadsResult(uploads);
    }
}
