using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitUploads;

public class GetVillageUnitUploadsQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetVillageUnitUploadsQuery, GetVillageUnitUploadsResult>
{
    public async Task<GetVillageUnitUploadsResult> Handle(
        GetVillageUnitUploadsQuery query,
        CancellationToken cancellationToken)
    {
        var uploads = await dbContext.VillageUnitUploads
            .Where(u => u.AppraisalId == query.AppraisalId)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new VillageUnitUploadDto(
                u.Id, u.AppraisalId, u.FileName, u.UploadedAt, u.IsUsed, u.DocumentId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new GetVillageUnitUploadsResult(uploads);
    }
}
