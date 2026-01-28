using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.RemoveMarketComparableImage;

/// <summary>
/// Handler for removing an image from a market comparable
/// </summary>
public class RemoveMarketComparableImageCommandHandler(
    IMarketComparableRepository marketComparableRepository
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

        comparable.RemoveImage(command.ImageId);

        return new RemoveMarketComparableImageResult(true);
    }
}
