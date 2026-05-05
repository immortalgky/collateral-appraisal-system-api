using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.AddProjectTowerImage;

/// <summary>
/// Command to add an image to a project tower.
/// References a gallery photo via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public record AddProjectTowerImageCommand(
    Guid TowerId,
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
) : ICommand<AddProjectTowerImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
