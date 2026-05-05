using Appraisal.Domain.Projects;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.UnsetProjectTowerImageThumbnail;

/// <summary>Handler for removing the thumbnail designation from a project tower image.</summary>
public class UnsetProjectTowerImageThumbnailCommandHandler(
    IProjectRepository projectRepository
) : ICommandHandler<UnsetProjectTowerImageThumbnailCommand, UnsetProjectTowerImageThumbnailResult>
{
    public async Task<UnsetProjectTowerImageThumbnailResult> Handle(
        UnsetProjectTowerImageThumbnailCommand command,
        CancellationToken cancellationToken)
    {
        var tower = await projectRepository.GetTowerByIdWithImagesAsync(
            command.TowerId,
            cancellationToken);

        if (tower is null)
            throw new InvalidOperationException($"Project tower with ID {command.TowerId} not found");

        tower.UnsetThumbnail(command.ImageId);

        return new UnsetProjectTowerImageThumbnailResult(command.ImageId);
    }
}
