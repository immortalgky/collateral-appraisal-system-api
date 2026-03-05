namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Request to add an image to a market comparable.
/// References a gallery photo from AppraisalGallery.
/// </summary>
public record AddMarketComparableImageRequest(
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
);
