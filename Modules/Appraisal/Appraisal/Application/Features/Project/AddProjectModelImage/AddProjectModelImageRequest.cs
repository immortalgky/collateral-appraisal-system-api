namespace Appraisal.Application.Features.Project.AddProjectModelImage;

/// <summary>
/// Request to add an image to a project model.
/// References a gallery photo from AppraisalGallery.
/// </summary>
public record AddProjectModelImageRequest(
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
);
