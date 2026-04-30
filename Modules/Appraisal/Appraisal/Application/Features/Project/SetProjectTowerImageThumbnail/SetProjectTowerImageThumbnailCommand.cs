using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.SetProjectTowerImageThumbnail;

/// <summary>Command to set an image as the thumbnail for a project tower.</summary>
public record SetProjectTowerImageThumbnailCommand(
    Guid TowerId,
    Guid ImageId
) : ICommand<SetProjectTowerImageThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
