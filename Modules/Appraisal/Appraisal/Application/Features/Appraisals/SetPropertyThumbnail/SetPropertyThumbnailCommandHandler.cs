namespace Appraisal.Application.Features.Appraisals.SetPropertyThumbnail;

public class SetPropertyThumbnailCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<SetPropertyThumbnailCommand, SetPropertyThumbnailResult>
{
    public async Task<SetPropertyThumbnailResult> Handle(
        SetPropertyThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var propertyMappings = (await galleryRepository.GetMappingsByPropertyIdAsync(
            command.AppraisalPropertyId, cancellationToken)).ToList();

        var mapping = propertyMappings.FirstOrDefault(m => m.GalleryPhotoId == command.GalleryPhotoId);

        if (mapping is null)
            throw new InvalidOperationException(
                $"Photo {command.GalleryPhotoId} is not linked to property {command.AppraisalPropertyId}");

        // Unset any existing thumbnail for the same property
        foreach (var m in propertyMappings)
            if (m.IsThumbnail)
                m.UnsetAsThumbnail();

        mapping.SetAsThumbnail();

        return new SetPropertyThumbnailResult(mapping.Id);
    }
}