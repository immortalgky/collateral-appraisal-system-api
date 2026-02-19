using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateGalleryPhoto;

public class UpdateGalleryPhotoCommandHandler(
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<UpdateGalleryPhotoCommand, UpdateGalleryPhotoResult>
{
    public async Task<UpdateGalleryPhotoResult> Handle(
        UpdateGalleryPhotoCommand command,
        CancellationToken cancellationToken)
    {
        var photo = await galleryRepository.GetByIdAsync(command.PhotoId, cancellationToken);

        if (photo is null)
            throw new InvalidOperationException($"Gallery photo with ID {command.PhotoId} not found");

        photo.SetDetails(command.PhotoCategory, command.Caption);

        if (command.Latitude.HasValue && command.Longitude.HasValue)
            photo.SetGps(command.Latitude.Value, command.Longitude.Value);

        if (command.CapturedAt.HasValue)
            photo.SetCapturedAt(command.CapturedAt.Value);

        return new UpdateGalleryPhotoResult(photo.Id);
    }
}
