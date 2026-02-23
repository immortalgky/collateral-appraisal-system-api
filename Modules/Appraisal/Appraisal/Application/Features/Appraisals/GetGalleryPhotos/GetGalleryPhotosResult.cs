namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public record GetGalleryPhotosResult(List<GalleryPhotoDto> Photos);

public record GalleryPhotoDto(
    Guid Id,
    Guid DocumentId,
    int PhotoNumber,
    string PhotoType,
    string? PhotoCategory,
    string? Caption,
    decimal? Latitude,
    decimal? Longitude,
    DateTime? CapturedAt,
    DateTime UploadedAt,
    bool IsUsedInReport,
    string? ReportSection,
    List<Guid> PhotoTopicIds
);
