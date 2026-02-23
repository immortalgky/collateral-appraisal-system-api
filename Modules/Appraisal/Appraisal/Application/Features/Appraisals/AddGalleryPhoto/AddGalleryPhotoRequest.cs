namespace Appraisal.Application.Features.Appraisals.AddGalleryPhoto;

public record AddGalleryPhotoRequest(
    Guid DocumentId,
    string PhotoType,
    string UploadedBy,
    string? PhotoCategory = null,
    string? Caption = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    DateTime? CapturedAt = null,
    List<Guid>? PhotoTopicIds = null
);
