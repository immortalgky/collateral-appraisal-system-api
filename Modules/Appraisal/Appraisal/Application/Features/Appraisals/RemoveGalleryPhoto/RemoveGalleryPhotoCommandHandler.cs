using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;

public class RemoveGalleryPhotoCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<RemoveGalleryPhotoCommand, RemoveGalleryPhotoResult>
{
    public async Task<RemoveGalleryPhotoResult> Handle(
        RemoveGalleryPhotoCommand command,
        CancellationToken cancellationToken)
    {
        var photo = await galleryRepository.GetByIdAsync(command.PhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.PhotoId} not found");

        // Delete any property photo mappings linked to this photo
        var mappings = await galleryRepository.GetMappingsByPhotoIdAsync(command.PhotoId, cancellationToken);
        foreach (var mapping in mappings)
        {
            await galleryRepository.DeleteMappingAsync(mapping, cancellationToken);
        }

        // Delete any topic mappings linked to this photo
        await galleryRepository.DeleteTopicMappingsByPhotoIdAsync(command.PhotoId, cancellationToken);

        await galleryRepository.DeleteAsync(photo, cancellationToken);

        return new RemoveGalleryPhotoResult(true);
    }
}
