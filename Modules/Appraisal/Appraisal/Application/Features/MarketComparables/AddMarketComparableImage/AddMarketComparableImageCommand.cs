using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Command to add an image to a market comparable.
/// References a document that was uploaded via the Document API.
/// </summary>
public record AddMarketComparableImageCommand(
    Guid MarketComparableId,
    Guid DocumentId,
    string? Title = null,
    string? Description = null
) : ICommand<AddMarketComparableImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
