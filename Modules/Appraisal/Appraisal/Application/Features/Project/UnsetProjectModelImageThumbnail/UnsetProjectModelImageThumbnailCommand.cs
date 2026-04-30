using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.UnsetProjectModelImageThumbnail;

/// <summary>Command to remove the thumbnail designation from a project model image.</summary>
public record UnsetProjectModelImageThumbnailCommand(
    Guid ModelId,
    Guid ImageId
) : ICommand<UnsetProjectModelImageThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
