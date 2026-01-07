using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

/// <summary>
/// Handler for creating a new Market Comparable
/// </summary>
public class CreateMarketComparableCommandHandler(
    IMarketComparableRepository marketComparableRepository
) : ICommandHandler<CreateMarketComparableCommand, CreateMarketComparableResult>
{
    public async Task<CreateMarketComparableResult> Handle(
        CreateMarketComparableCommand command,
        CancellationToken cancellationToken)
    {
        var comparable = MarketComparable.Create(
            command.ComparableNumber,
            command.PropertyType,
            command.Province,
            command.DataSource,
            command.SurveyDate);

        if (command.District is not null || command.SubDistrict is not null ||
            command.Address is not null || command.Latitude.HasValue || command.Longitude.HasValue)
            comparable.SetLocation(
                command.District,
                command.SubDistrict,
                command.Address,
                command.Latitude,
                command.Longitude);

        if (command.TransactionType is not null || command.TransactionDate.HasValue ||
            command.TransactionPrice.HasValue || command.PricePerUnit.HasValue || command.UnitType is not null)
            comparable.SetTransaction(
                command.TransactionType,
                command.TransactionDate,
                command.TransactionPrice,
                command.PricePerUnit,
                command.UnitType);

        await marketComparableRepository.AddAsync(comparable, cancellationToken);

        return new CreateMarketComparableResult(comparable.Id);
    }
}