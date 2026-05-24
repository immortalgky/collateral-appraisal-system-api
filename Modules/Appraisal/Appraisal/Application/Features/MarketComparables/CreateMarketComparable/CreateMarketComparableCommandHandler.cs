using Appraisal.Domain.MarketComparables;
using Shared.CQRS;
using Shared.Identity;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

/// <summary>
/// Handler for creating a new Market Comparable
/// </summary>
public class CreateMarketComparableCommandHandler(
    IMarketComparableRepository marketComparableRepository,
    ICurrentUserService currentUser
) : ICommandHandler<CreateMarketComparableCommand, CreateMarketComparableResult>
{
    public async Task<CreateMarketComparableResult> Handle(
        CreateMarketComparableCommand command,
        CancellationToken cancellationToken)
    {
        var comparable = MarketComparable.Create(
            command.PropertyType,
            command.SurveyName,
            command.InfoDateTime,
            command.SourceInfo,
            command.TemplateId,
            command.Notes,
            command.OfferPrice,
            command.OfferPriceAdjustmentPercent,
            command.OfferPriceAdjustmentAmount,
            command.SalePrice,
            command.SaleDate,
            command.OfferPriceUnit,
            command.SalePriceUnit,
            command.Latitude,
            command.Longitude,
            createdByCompanyId: currentUser.CompanyId);

        await marketComparableRepository.AddAsync(comparable, cancellationToken);

        return new CreateMarketComparableResult(comparable.Id);
    }
}