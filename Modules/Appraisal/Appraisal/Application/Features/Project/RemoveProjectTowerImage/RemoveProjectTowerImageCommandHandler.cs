using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.RemoveProjectTowerImage;

/// <summary>Handler for removing an image from a project tower.</summary>
public class RemoveProjectTowerImageCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<RemoveProjectTowerImageCommand, RemoveProjectTowerImageResult>
{
    public async Task<RemoveProjectTowerImageResult> Handle(
        RemoveProjectTowerImageCommand command,
        CancellationToken cancellationToken)
    {
        var tower = await projectRepository.GetTowerByIdWithImagesAsync(
            command.TowerId,
            cancellationToken);

        if (tower is null)
            throw new InvalidOperationException($"Project tower with ID {command.TowerId} not found");

        // Get GalleryPhotoId before removing the image
        var image = tower.Images.FirstOrDefault(i => i.Id == command.ImageId);
        var galleryPhotoId = image?.GalleryPhotoId;

        tower.RemoveImage(command.ImageId);

        // Check if photo is still linked anywhere; if not, mark as not in use
        if (galleryPhotoId.HasValue)
        {
            var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(
                galleryPhotoId.Value, cancellationToken);
            if (!stillLinked)
            {
                var photo = await galleryRepository.GetByIdAsync(galleryPhotoId.Value, cancellationToken);
                photo?.MarkAsNotInUse();
            }
        }

        return new RemoveProjectTowerImageResult(true);
    }
}
