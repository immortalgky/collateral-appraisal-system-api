namespace Appraisal.Application.Features.Appraisals.UpdateGalleryPhoto;

public record UpdateGalleryPhotoRequest(
    string? PhotoCategory,
    string? Caption,
    decimal? Latitude,
    decimal? Longitude,
    DateTime? CapturedAt
);
