using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.SetProjectModelImageThumbnail;

/// <summary>
/// Handler for setting a project model image as the thumbnail.
/// The single-thumbnail invariant is enforced inside ProjectModel.SetThumbnail().
/// </summary>
public class SetProjectModelImageThumbnailCommandHandler(
    IProjectRepository projectRepository
) : ICommandHandler<SetProjectModelImageThumbnailCommand, SetProjectModelImageThumbnailResult>
{
    public async Task<SetProjectModelImageThumbnailResult> Handle(
        SetProjectModelImageThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var model = await projectRepository.GetModelByIdWithImagesAsync(
            command.ModelId,
            cancellationToken);

        if (model is null)
            throw new InvalidOperationException($"Project model with ID {command.ModelId} not found");

        model.SetThumbnail(command.ImageId);

        return new SetProjectModelImageThumbnailResult(command.ImageId);
    }
}
