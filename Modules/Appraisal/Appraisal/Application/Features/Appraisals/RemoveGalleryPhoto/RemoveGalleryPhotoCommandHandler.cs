using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;

public class RemoveGalleryPhotoCommandHandler(
    IAppraisalGalleryRepository galleryRepository,
    IIntegrationEventOutbox outbox
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
        var mappings = (await galleryRepository.GetMappingsByPhotoIdAsync(command.PhotoId, cancellationToken)).ToList();

        // Track which properties will lose their thumbnail
        var affectedPropertyIds = mappings
            .Where(m => m.IsThumbnail)
            .Select(m => m.AppraisalPropertyId)
            .ToList();

        foreach (var mapping in mappings)
        {
            await galleryRepository.DeleteMappingAsync(mapping, cancellationToken);
        }

        // Auto-promote another photo as thumbnail for each affected property
        foreach (var propertyId in affectedPropertyIds)
        {
            var remaining = await galleryRepository.GetMappingsByPropertyIdAsync(propertyId, cancellationToken);
            var next = remaining.FirstOrDefault();
            next?.SetAsThumbnail();
        }

        // Delete any topic mappings linked to this photo
        await galleryRepository.DeleteTopicMappingsByPhotoIdAsync(command.PhotoId, cancellationToken);

        var documentId = photo.DocumentId;

        await galleryRepository.DeleteAsync(photo, cancellationToken);

        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(command.PhotoId, documentId));

        return new RemoveGalleryPhotoResult(true);
    }
}
