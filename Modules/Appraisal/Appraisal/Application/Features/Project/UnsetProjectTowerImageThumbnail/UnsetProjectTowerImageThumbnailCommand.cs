using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.UnsetProjectTowerImageThumbnail;

/// <summary>Command to remove the thumbnail designation from a project tower image.</summary>
public record UnsetProjectTowerImageThumbnailCommand(
    Guid TowerId,
    Guid ImageId
) : ICommand<UnsetProjectTowerImageThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
