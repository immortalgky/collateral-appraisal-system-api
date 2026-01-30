using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.RemoveMarketComparableImage;

/// <summary>
/// Command to remove an image from a market comparable
/// </summary>
public record RemoveMarketComparableImageCommand(
    Guid MarketComparableId,
    Guid ImageId
) : ICommand<RemoveMarketComparableImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
