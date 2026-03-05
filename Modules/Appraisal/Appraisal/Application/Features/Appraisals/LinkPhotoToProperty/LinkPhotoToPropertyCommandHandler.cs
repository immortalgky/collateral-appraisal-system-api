using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.LinkPhotoToProperty;

public class LinkPhotoToPropertyCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<LinkPhotoToPropertyCommand, LinkPhotoToPropertyResult>
{
    public async Task<LinkPhotoToPropertyResult> Handle(
        LinkPhotoToPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // Verify gallery photo exists
        var photo = await galleryRepository.GetByIdAsync(command.GalleryPhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.GalleryPhotoId} not found");

        var mapping = PropertyPhotoMapping.Create(
            command.GalleryPhotoId,
            command.AppraisalPropertyId,
            command.PhotoPurpose,
            command.LinkedBy);

        if (command.SectionReference is not null)
            mapping.SetSection(command.SectionReference);

        // Check for duplicate mapping
        var existingMappings = await galleryRepository.GetMappingsByPropertyIdAsync(
            command.AppraisalPropertyId, cancellationToken);

        if (existingMappings.Any(m => m.GalleryPhotoId == command.GalleryPhotoId))
            throw new InvalidOperationException("This photo is already linked to this property");

        // Auto-set as thumbnail if this is the first photo linked to the property
        if (!existingMappings.Any())
            mapping.SetAsThumbnail();

        await galleryRepository.AddMappingAsync(mapping, cancellationToken);

        photo.MarkAsInUse();

        return new LinkPhotoToPropertyResult(mapping.Id);
    }
}
