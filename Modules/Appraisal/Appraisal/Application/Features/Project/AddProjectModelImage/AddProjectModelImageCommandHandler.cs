using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.AddProjectModelImage;

/// <summary>Handler for adding an image to a project model.</summary>
public class AddProjectModelImageCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<AddProjectModelImageCommand, AddProjectModelImageResult>
{
    public async Task<AddProjectModelImageResult> Handle(
        AddProjectModelImageCommand command,
        CancellationToken cancellationToken)
    {
        var model = await projectRepository.GetModelByIdWithImagesAsync(
            command.ModelId,
            cancellationToken);

        if (model is null)
            throw new InvalidOperationException($"Project model with ID {command.ModelId} not found");

        var image = model.AddImage(
            command.GalleryPhotoId,
            command.Title,
            command.Description);

        // Mark gallery photo as in use
        var photo = await galleryRepository.GetByIdAsync(command.GalleryPhotoId, cancellationToken);
        photo?.MarkAsInUse();

        return new AddProjectModelImageResult(image.Id);
    }
}
