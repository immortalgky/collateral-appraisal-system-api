using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnsetPropertyThumbnail;

public class UnsetPropertyThumbnailCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<UnsetPropertyThumbnailCommand, UnsetPropertyThumbnailResult>
{
    public async Task<UnsetPropertyThumbnailResult> Handle(
        UnsetPropertyThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var propertyMappings = await galleryRepository.GetMappingsByPropertyIdAsync(
            command.AppraisalPropertyId, cancellationToken);

        var mapping = propertyMappings.FirstOrDefault(m => m.GalleryPhotoId == command.GalleryPhotoId);

        if (mapping is null)
            throw new InvalidOperationException(
                $"Photo {command.GalleryPhotoId} is not linked to property {command.AppraisalPropertyId}");

        mapping.UnsetAsThumbnail();

        return new UnsetPropertyThumbnailResult(mapping.Id);
    }
}
