using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface IAppraisalGalleryRepository : IRepository<AppraisalGallery, Guid>
{
    Task<IEnumerable<AppraisalGallery>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken ct = default);
    Task<int> GetMaxPhotoNumberAsync(Guid appraisalId, CancellationToken ct = default);
    Task<bool> IsPhotoLinkedAnywhereAsync(Guid galleryPhotoId, CancellationToken ct = default);

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
