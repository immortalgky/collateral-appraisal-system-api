using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Appraisal.Infrastructure.Repositories;

public class AppraisalGalleryRepository(AppraisalDbContext dbContext)
    : BaseRepository<AppraisalGallery, Guid>(dbContext), IAppraisalGalleryRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<IEnumerable<AppraisalGallery>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.AppraisalGallery
            .Where(g => g.AppraisalId == appraisalId)
            .OrderBy(g => g.PhotoNumber)
            .ToListAsync(ct);
    }

    public async Task<int> GetMaxPhotoNumberAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var max = await _dbContext.AppraisalGallery
            .Where(g => g.AppraisalId == appraisalId)
            .MaxAsync(g => (int?)g.PhotoNumber, ct);

        return max ?? 0;
    }

    public async Task<PropertyPhotoMapping?> GetMappingByIdAsync(Guid mappingId, CancellationToken ct = default)
    {
        return await _dbContext.PropertyPhotoMappings.FindAsync([mappingId], ct);
    }

    public async Task<IEnumerable<PropertyPhotoMapping>> GetMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default)
    {
        return await _dbContext.PropertyPhotoMappings
            .Where(m => m.GalleryPhotoId == galleryPhotoId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<PropertyPhotoMapping>> GetMappingsByPropertyIdAsync(Guid propertyId, CancellationToken ct = default)
    {
        return await _dbContext.PropertyPhotoMappings
            .Where(m => m.AppraisalPropertyId == propertyId)
            .ToListAsync(ct);
    }

    public async Task AddMappingAsync(PropertyPhotoMapping mapping, CancellationToken ct = default)
    {
        await _dbContext.PropertyPhotoMappings.AddAsync(mapping, ct);
    }

    public Task DeleteMappingAsync(PropertyPhotoMapping mapping, CancellationToken ct = default)
    {
        _dbContext.PropertyPhotoMappings.Remove(mapping);
        return Task.CompletedTask;
    }
}
