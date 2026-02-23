using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.SetMarketComparableFactorData;

/// <summary>
/// Handler for setting factor data for a market comparable
/// </summary>
public class SetMarketComparableFactorDataCommandHandler(
    IMarketComparableRepository marketComparableRepository
) : ICommandHandler<SetMarketComparableFactorDataCommand, SetMarketComparableFactorDataResult>
{
    public async Task<SetMarketComparableFactorDataResult> Handle(
        SetMarketComparableFactorDataCommand command,
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

        foreach (var item in command.FactorData)
        {
            comparable.SetFactorValue(item.FactorId, item.Value, item.OtherRemarks);
        }

        return new SetMarketComparableFactorDataResult(true);
    }
}
