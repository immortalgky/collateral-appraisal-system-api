using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Handler for adding an image to a market comparable
/// </summary>
public class AddMarketComparableImageCommandHandler(
    IMarketComparableRepository marketComparableRepository
) : ICommandHandler<AddMarketComparableImageCommand, AddMarketComparableImageResult>
{
    public async Task<AddMarketComparableImageResult> Handle(
        AddMarketComparableImageCommand command,
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

        var image = comparable.AddImage(
            command.DocumentId,
            command.Title,
            command.Description);

        return new AddMarketComparableImageResult(image.Id);
    }
}
