using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.UnsetProjectModelImageThumbnail;

/// <summary>Handler for removing the thumbnail designation from a project model image.</summary>
public class UnsetProjectModelImageThumbnailCommandHandler(
    IProjectRepository projectRepository
) : ICommandHandler<UnsetProjectModelImageThumbnailCommand, UnsetProjectModelImageThumbnailResult>
{
    public async Task<UnsetProjectModelImageThumbnailResult> Handle(
        UnsetProjectModelImageThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var model = await projectRepository.GetModelByIdWithImagesAsync(
            command.ModelId,
            cancellationToken);

        if (model is null)
            throw new InvalidOperationException($"Project model with ID {command.ModelId} not found");

        model.UnsetThumbnail(command.ImageId);

        return new UnsetProjectModelImageThumbnailResult(command.ImageId);
    }
}
