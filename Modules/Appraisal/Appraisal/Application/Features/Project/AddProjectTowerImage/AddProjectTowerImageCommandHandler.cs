using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.AddProjectTowerImage;

/// <summary>Handler for adding an image to a project tower.</summary>
public class AddProjectTowerImageCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<AddProjectTowerImageCommand, AddProjectTowerImageResult>
{
    public async Task<AddProjectTowerImageResult> Handle(
        AddProjectTowerImageCommand command,
        CancellationToken cancellationToken)
    {
        var tower = await projectRepository.GetTowerByIdWithImagesAsync(
            command.TowerId,
            cancellationToken);

        if (tower is null)
            throw new InvalidOperationException($"Project tower with ID {command.TowerId} not found");

        var image = tower.AddImage(
            command.GalleryPhotoId,
            command.Title,
            command.Description);

        // Mark gallery photo as in use
        var photo = await galleryRepository.GetByIdAsync(command.GalleryPhotoId, cancellationToken);
        photo?.MarkAsInUse();

        return new AddProjectTowerImageResult(image.Id);
    }
}
