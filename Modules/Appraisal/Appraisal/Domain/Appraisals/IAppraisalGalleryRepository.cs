using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface IAppraisalGalleryRepository : IRepository<AppraisalGallery, Guid>
{
    Task<IEnumerable<AppraisalGallery>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);
    Task<IEnumerable<AppraisalGallery>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<int> GetMaxPhotoNumberAsync(Guid appraisalId, CancellationToken ct = default);
    Task<bool> IsPhotoLinkedAnywhereAsync(Guid galleryPhotoId, CancellationToken ct = default);

    /// <summary>
    /// Batched form of <see cref="IsPhotoLinkedAnywhereAsync"/>: returns the subset of
    /// <paramref name="galleryPhotoIds"/> that are still referenced by any linkable entity. Flushes
    /// pending changes first (so just-removed mappings don't count), then checks all link tables with
    /// one query each — a fixed number of round-trips regardless of how many photos are passed.
    /// </summary>
    Task<HashSet<Guid>> GetPhotosLinkedElsewhereAsync(IReadOnlyCollection<Guid> galleryPhotoIds, CancellationToken ct = default);

    // PropertyPhotoMapping operations
    Task<PropertyPhotoMapping?> GetMappingByIdAsync(Guid mappingId, CancellationToken ct = default);
    Task<IEnumerable<PropertyPhotoMapping>> GetMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default);
    Task<IEnumerable<PropertyPhotoMapping>> GetMappingsByPropertyIdAsync(Guid propertyId, CancellationToken ct = default);
    Task AddMappingAsync(PropertyPhotoMapping mapping, CancellationToken ct = default);
    Task DeleteMappingAsync(PropertyPhotoMapping mapping, CancellationToken ct = default);

    // GalleryPhotoTopicMapping operations
    Task<IEnumerable<GalleryPhotoTopicMapping>> GetTopicMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default);
    Task AddTopicMappingAsync(GalleryPhotoTopicMapping mapping, CancellationToken ct = default);
    Task DeleteTopicMappingAsync(GalleryPhotoTopicMapping mapping, CancellationToken ct = default);
    Task DeleteTopicMappingsByPhotoIdAsync(Guid galleryPhotoId, CancellationToken ct = default);
}
