using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.UpdateMarketComparableTemplate;

public class UpdateMarketComparableTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<UpdateMarketComparableTemplateCommand, UpdateMarketComparableTemplateResult>
{
    public async Task<UpdateMarketComparableTemplateResult> Handle(
        UpdateMarketComparableTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Market comparable template with ID {command.Id} not found.");
        }

        template.Update(
            command.TemplateName,
            command.Description);

        await repository.UpdateAsync(template, cancellationToken);

        return new UpdateMarketComparableTemplateResult(template.Id);
    }
}
