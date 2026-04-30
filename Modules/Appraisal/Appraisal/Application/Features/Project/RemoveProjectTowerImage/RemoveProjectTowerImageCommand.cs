using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.RemoveProjectTowerImage;

/// <summary>Command to remove an image from a project tower.</summary>
public record RemoveProjectTowerImageCommand(
    Guid TowerId,
    Guid ImageId
) : ICommand<RemoveProjectTowerImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
