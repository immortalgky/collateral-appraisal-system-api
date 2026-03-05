using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Command to add an image to a market comparable.
/// References a gallery photo via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public record AddMarketComparableImageCommand(
    Guid MarketComparableId,
    Guid GalleryPhotoId,
    string? Title = null,
    string? Description = null
) : ICommand<AddMarketComparableImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
