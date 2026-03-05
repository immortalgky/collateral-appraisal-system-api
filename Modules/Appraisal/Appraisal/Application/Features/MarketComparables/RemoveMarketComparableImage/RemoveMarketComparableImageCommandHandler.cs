using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.RemoveMarketComparableImage;

/// <summary>
/// Handler for removing an image from a market comparable
/// </summary>
public class RemoveMarketComparableImageCommandHandler(
    IMarketComparableRepository marketComparableRepository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<RemoveMarketComparableImageCommand, RemoveMarketComparableImageResult>
{
    public async Task<RemoveMarketComparableImageResult> Handle(
        RemoveMarketComparableImageCommand command,
        CancellationToken cancellationToken)
    {
        var comparable = await marketComparableRepository.GetByIdWithDetailsAsync(
            command.MarketComparableId,
            cancellationToken);

        if (comparable is null)
        {
            throw new InvalidOperationException(
                $"Market comparable with ID {command.MarketComparableId} not found");
        }

        // Get GalleryPhotoId before removing the image
        var image = comparable.Images.FirstOrDefault(i => i.Id == command.ImageId);
        var galleryPhotoId = image?.GalleryPhotoId;

        comparable.RemoveImage(command.ImageId);

        // Check if photo is still linked anywhere; if not, mark as not in use
        if (galleryPhotoId.HasValue)
        {
            var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(galleryPhotoId.Value, cancellationToken);
            if (!stillLinked)
            {
                var photo = await galleryRepository.GetByIdAsync(galleryPhotoId.Value, cancellationToken);
                photo?.MarkAsNotInUse();
            }
        }

        return new RemoveMarketComparableImageResult(true);
    }
}
