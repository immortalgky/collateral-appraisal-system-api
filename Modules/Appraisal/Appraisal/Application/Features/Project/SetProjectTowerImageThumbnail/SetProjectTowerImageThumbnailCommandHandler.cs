using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.SetProjectTowerImageThumbnail;

/// <summary>
/// Handler for setting a project tower image as the thumbnail.
/// The single-thumbnail invariant is enforced inside ProjectTower.SetThumbnail().
/// </summary>
public class SetProjectTowerImageThumbnailCommandHandler(
    IProjectRepository projectRepository
) : ICommandHandler<SetProjectTowerImageThumbnailCommand, SetProjectTowerImageThumbnailResult>
{
    public async Task<SetProjectTowerImageThumbnailResult> Handle(
        SetProjectTowerImageThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var tower = await projectRepository.GetTowerByIdWithImagesAsync(
            command.TowerId,
            cancellationToken);

        if (tower is null)
            throw new InvalidOperationException($"Project tower with ID {command.TowerId} not found");

        tower.SetThumbnail(command.ImageId);

        return new SetProjectTowerImageThumbnailResult(command.ImageId);
    }
}
