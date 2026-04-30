using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.RemoveProjectModelImage;

/// <summary>Handler for removing an image from a project model.</summary>
public class RemoveProjectModelImageCommandHandler(
    IProjectRepository projectRepository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<RemoveProjectModelImageCommand, RemoveProjectModelImageResult>
{
    public async Task<RemoveProjectModelImageResult> Handle(
        RemoveProjectModelImageCommand command,
        CancellationToken cancellationToken)
    {
        var model = await projectRepository.GetModelByIdWithImagesAsync(
            command.ModelId,
            cancellationToken);

        if (model is null)
            throw new InvalidOperationException($"Project model with ID {command.ModelId} not found");

        // Get GalleryPhotoId before removing the image
        var image = model.Images.FirstOrDefault(i => i.Id == command.ImageId);
        var galleryPhotoId = image?.GalleryPhotoId;

        model.RemoveImage(command.ImageId);

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

        return new RemoveProjectModelImageResult(true);
    }
}
