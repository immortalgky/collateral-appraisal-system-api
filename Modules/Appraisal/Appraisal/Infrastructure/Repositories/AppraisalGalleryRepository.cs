using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
using Appraisal.Domain.Projects;
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

    public async Task<IEnumerable<AppraisalGallery>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await _dbContext.AppraisalGallery
            .Where(g => ids.Contains(g.Id))
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
        // Single-photo form delegates to the batched check so the "what counts as a link" rule
        // (the set of link tables below) lives in exactly one place.
        var linked = await GetPhotosLinkedElsewhereAsync([galleryPhotoId], ct);
        return linked.Contains(galleryPhotoId);
    }

    public async Task<HashSet<Guid>> GetPhotosLinkedElsewhereAsync(
        IReadOnlyCollection<Guid> galleryPhotoIds, CancellationToken ct = default)
    {
        var linked = new HashSet<Guid>();

        if (galleryPhotoIds.Count == 0)
            return linked;

        // Flush pending changes (e.g. mapping deletions) to DB so the check reflects current state.
        // Runs within the existing transaction from the pipeline behavior — no commit yet.
        await _dbContext.SaveChangesAsync(ct);

        // One query per link table (IN over the id set) instead of a per-photo scan. Each collects
        // the ids that ARE referenced; their union is the set of still-linked photos.
        linked.UnionWith(await _dbContext.PropertyPhotoMappings
            .Where(m => galleryPhotoIds.Contains(m.GalleryPhotoId))
            .Select(m => m.GalleryPhotoId).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.GalleryPhotoTopicMappings
            .Where(m => galleryPhotoIds.Contains(m.GalleryPhotoId))
            .Select(m => m.GalleryPhotoId).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.Set<AppendixDocument>()
            .Where(d => d.GalleryPhotoId != null && galleryPhotoIds.Contains(d.GalleryPhotoId.Value))
            .Select(d => d.GalleryPhotoId!.Value).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.Set<MarketComparableImage>()
            .Where(i => galleryPhotoIds.Contains(i.GalleryPhotoId))
            .Select(i => i.GalleryPhotoId).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.Set<LawAndRegulationImage>()
            .Where(i => galleryPhotoIds.Contains(i.GalleryPhotoId))
            .Select(i => i.GalleryPhotoId).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.Set<ProjectModelImage>()
            .Where(i => galleryPhotoIds.Contains(i.GalleryPhotoId))
            .Select(i => i.GalleryPhotoId).Distinct().ToListAsync(ct));
        linked.UnionWith(await _dbContext.Set<ProjectTowerImage>()
            .Where(i => galleryPhotoIds.Contains(i.GalleryPhotoId))
            .Select(i => i.GalleryPhotoId).Distinct().ToListAsync(ct));

        return linked;
    }
}
