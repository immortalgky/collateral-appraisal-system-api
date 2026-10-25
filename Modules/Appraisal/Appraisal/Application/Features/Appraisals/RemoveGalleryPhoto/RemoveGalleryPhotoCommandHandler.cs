using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Shared.CQRS;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;

public class RemoveGalleryPhotoCommandHandler(
    IAppraisalGalleryRepository galleryRepository,
    IProjectRepository projectRepository,
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

        // And any law & regulation image showing it — the law editor would otherwise keep a tile
        // for a photo that no longer exists.
        await galleryRepository.DeleteLawImagesByPhotoIdAsync(command.PhotoId, cancellationToken);

        // Block tower / model photos hold the gallery photo id with no foreign key, so unlink them here;
        // a dangling cover row would stop the next photo added from becoming the cover.
        foreach (var project in await projectRepository.GetByGalleryPhotoAsync(command.PhotoId, cancellationToken))
            project.RemoveImagesOfPhoto(command.PhotoId);

        var documentId = photo.DocumentId;

        await galleryRepository.DeleteAsync(photo, cancellationToken);

        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(command.PhotoId, documentId));

        return new RemoveGalleryPhotoResult(true);
    }
}
