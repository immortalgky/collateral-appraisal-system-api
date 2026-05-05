using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.SetProjectModelImageThumbnail;

/// <summary>Command to set an image as the thumbnail for a project model.</summary>
public record SetProjectModelImageThumbnailCommand(
    Guid ModelId,
    Guid ImageId
) : ICommand<SetProjectModelImageThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
