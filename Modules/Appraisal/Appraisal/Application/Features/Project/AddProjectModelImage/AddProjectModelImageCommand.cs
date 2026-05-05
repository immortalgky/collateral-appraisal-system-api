using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Project.AddProjectModelImage;

/// <summary>
/// Command to add an image to a project model.
/// References a gallery photo via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public record AddProjectModelImageCommand(
    Guid ModelId,
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
) : ICommand<AddProjectModelImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
