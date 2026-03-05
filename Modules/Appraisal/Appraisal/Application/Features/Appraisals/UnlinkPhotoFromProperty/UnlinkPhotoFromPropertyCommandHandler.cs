using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnlinkPhotoFromProperty;

public class UnlinkPhotoFromPropertyCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<UnlinkPhotoFromPropertyCommand, UnlinkPhotoFromPropertyResult>
{
    public async Task<UnlinkPhotoFromPropertyResult> Handle(
        UnlinkPhotoFromPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var mapping = await galleryRepository.GetMappingByIdAsync(command.MappingId, cancellationToken);

        if (mapping is null)
            throw new InvalidOperationException($"Property photo mapping with ID {command.MappingId} not found");

        var wasThumbnail = mapping.IsThumbnail;
        var propertyId = mapping.AppraisalPropertyId;
        var galleryPhotoId = mapping.GalleryPhotoId;

        await galleryRepository.DeleteMappingAsync(mapping, cancellationToken);

        // Auto-promote another photo as thumbnail if the removed one was the thumbnail
        if (wasThumbnail)
        {
            var remaining = await galleryRepository.GetMappingsByPropertyIdAsync(propertyId, cancellationToken);
            var next = remaining.FirstOrDefault();
            next?.SetAsThumbnail();
        }

        // Check if photo is still linked anywhere; if not, mark as not in use
        var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(galleryPhotoId, cancellationToken);
        if (!stillLinked)
        {
            var photo = await galleryRepository.GetByIdAsync(galleryPhotoId, cancellationToken);
            photo?.MarkAsNotInUse();
        }

        return new UnlinkPhotoFromPropertyResult(true);
    }
}
