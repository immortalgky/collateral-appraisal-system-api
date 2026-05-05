using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.RemoveProjectModelImage;

/// <summary>Command to remove an image from a project model.</summary>
public record RemoveProjectModelImageCommand(
    Guid ModelId,
    Guid ImageId
) : ICommand<RemoveProjectModelImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
