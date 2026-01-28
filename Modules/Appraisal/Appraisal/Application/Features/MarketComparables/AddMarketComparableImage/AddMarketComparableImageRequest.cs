namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Request to add an image to a market comparable.
/// Images are uploaded via the Document API first, then linked by DocumentId.
/// </summary>
public record AddMarketComparableImageRequest(
    Guid DocumentId,
    string? Title = null,
    string? Description = null
);
