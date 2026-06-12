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
            throw new NotFoundException("ProjectTower", command.TowerId);

        tower.SetThumbnail(command.ImageId);

        return new SetProjectTowerImageThumbnailResult(command.ImageId);
    }
}
