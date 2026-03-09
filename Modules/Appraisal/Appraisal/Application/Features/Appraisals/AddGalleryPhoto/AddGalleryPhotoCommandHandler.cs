using Appraisal.Domain.Appraisals;
using MassTransit;
using Shared.CQRS;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Appraisals.AddGalleryPhoto;

public class AddGalleryPhotoCommandHandler(
    IAppraisalGalleryRepository galleryRepository,
    IBus bus
) : ICommandHandler<AddGalleryPhotoCommand, AddGalleryPhotoResult>
{
    public async Task<AddGalleryPhotoResult> Handle(
        AddGalleryPhotoCommand command,
        CancellationToken cancellationToken)
    {
        var maxNumber = await galleryRepository.GetMaxPhotoNumberAsync(
            command.AppraisalId, cancellationToken);

        var photoNumber = maxNumber + 1;

        var photo = AppraisalGallery.Create(
            command.AppraisalId,
            command.DocumentId,
            photoNumber,
            command.PhotoType,
            command.UploadedBy,
            command.FileName,
            command.FilePath,
            command.FileExtension,
            command.MimeType,
            command.FileSizeBytes,
            command.UploadedByName);

        if (command.PhotoCategory is not null || command.Caption is not null)
            photo.SetDetails(command.PhotoCategory, command.Caption);

        if (command.Latitude.HasValue && command.Longitude.HasValue)
            photo.SetGps(command.Latitude.Value, command.Longitude.Value);

        if (command.CapturedAt.HasValue)
            photo.SetCapturedAt(command.CapturedAt.Value);

        await galleryRepository.AddAsync(photo, cancellationToken);

        // Create topic mappings if provided
        if (command.PhotoTopicIds is { Count: > 0 })
        {
            foreach (var topicId in command.PhotoTopicIds)
            {
                var mapping = GalleryPhotoTopicMapping.Create(photo.Id, topicId);
                await galleryRepository.AddTopicMappingAsync(mapping, cancellationToken);
            }
        }

        await bus.Publish(
            new DocumentLinkedIntegrationEventV2(command.AppraisalId, command.DocumentId),
            cancellationToken);

        return new AddGalleryPhotoResult(photo.Id, photo.PhotoNumber);
    }
}
