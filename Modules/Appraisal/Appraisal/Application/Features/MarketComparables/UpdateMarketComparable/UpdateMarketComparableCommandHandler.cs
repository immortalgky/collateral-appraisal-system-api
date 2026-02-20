using System;
using static Appraisal.Domain.MarketComparables.MarketComparable;

namespace Appraisal.Application.Features.MarketComparables.UpdateMarketComparable;

public class UpdateMarketComparableCommandHandler(
    IMarketComparableRepository marketComparableRepository
) : ICommandHandler<UpdateMarketComparableCommand, UpdateMarketComparableResult>
{
    public async Task<UpdateMarketComparableResult> Handle(UpdateMarketComparableCommand command, CancellationToken cancellationToken)
    {
        await UpdateMarketComparableAsync(command, cancellationToken);
        return new UpdateMarketComparableResult(true);
    }

    public async Task<Domain.MarketComparables.MarketComparable> UpdateMarketComparableAsync(
        UpdateMarketComparableCommand command,
        CancellationToken cancellationToken)
    {
        var marketComparable = await marketComparableRepository.GetByIdWithDetailsAsync(command.Id, cancellationToken);
        if (marketComparable is null) throw new NotFoundException("MarketComparable Not Found: ", command.Id);

        marketComparable.Save(new MarketComparableUpdateData(
            command.SurveyName,
            command.InfoDateTime,
            command.SourceInfo,
            command.TemplateId,
            command.Notes
            )
        );

        await marketComparableRepository.UpdateAsync(marketComparable, cancellationToken);

        return marketComparable;
    }
}
