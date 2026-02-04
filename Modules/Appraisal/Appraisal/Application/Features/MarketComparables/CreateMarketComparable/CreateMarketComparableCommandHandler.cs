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
            command.SurveyName,
            command.InfoDateTime,
            command.SourceInfo,
            command.TemplateId,
            command.Notes
            );

        await marketComparableRepository.AddAsync(comparable, cancellationToken);

        return new CreateMarketComparableResult(comparable.Id);
    }
}