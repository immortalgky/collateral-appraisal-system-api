using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.DeleteMarketComparableTemplate;

public class DeleteMarketComparableTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<DeleteMarketComparableTemplateCommand, DeleteMarketComparableTemplateResult>
{
    public async Task<DeleteMarketComparableTemplateResult> Handle(
        DeleteMarketComparableTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Market comparable template with ID {command.Id} not found.");
        }

        await repository.DeleteAsync(template, cancellationToken);

        return new DeleteMarketComparableTemplateResult(true);
    }
}
