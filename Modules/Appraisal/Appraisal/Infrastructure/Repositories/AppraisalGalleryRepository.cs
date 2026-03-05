using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
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

    public async Task<IEnumerable<GalleryPhotoTopicMapping>> GetTopicMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default)
    {
        return await _dbContext.GalleryPhotoTopicMappings
            .Where(m => m.GalleryPhotoId == galleryPhotoId)
            .ToListAsync(ct);
    }

    public async Task AddTopicMappingAsync(GalleryPhotoTopicMapping mapping, CancellationToken ct = default)
    {
        await _dbContext.GalleryPhotoTopicMappings.AddAsync(mapping, ct);
    }

    public Task DeleteTopicMappingAsync(GalleryPhotoTopicMapping mapping, CancellationToken ct = default)
    {
        _dbContext.GalleryPhotoTopicMappings.Remove(mapping);
        return Task.CompletedTask;
    }

    public async Task DeleteTopicMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default)
    {
        var mappings = await _dbContext.GalleryPhotoTopicMappings
            .Where(m => m.GalleryPhotoId == galleryPhotoId)
            .ToListAsync(ct);

        _dbContext.GalleryPhotoTopicMappings.RemoveRange(mappings);
    }

    public async Task<bool> IsPhotoLinkedAnywhereAsync(Guid galleryPhotoId, CancellationToken ct = default)
    {
        // Flush pending changes (e.g. deletions) to DB so the check reflects current state.
        // Runs within the existing transaction from the pipeline behavior — no commit yet.
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.PropertyPhotoMappings
                   .AnyAsync(m => m.GalleryPhotoId == galleryPhotoId, ct)
               || await _dbContext.GalleryPhotoTopicMappings
                   .AnyAsync(m => m.GalleryPhotoId == galleryPhotoId, ct)
               || await _dbContext.Set<AppendixDocument>()
                   .AnyAsync(d => d.GalleryPhotoId == galleryPhotoId, ct)
               || await _dbContext.Set<MarketComparableImage>()
                   .AnyAsync(i => i.GalleryPhotoId == galleryPhotoId, ct)
               || await _dbContext.Set<LawAndRegulationImage>()
                   .AnyAsync(i => i.GalleryPhotoId == galleryPhotoId, ct);
    }
}
