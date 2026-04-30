namespace Appraisal.Application.Features.Project.AddProjectTowerImage;

/// <summary>
/// Request to add an image to a project tower.
/// References a gallery photo from AppraisalGallery.
/// </summary>
public record AddProjectTowerImageRequest(
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
);
