using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.RemoveFactorFromTemplate;

public class RemoveFactorFromTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<RemoveFactorFromTemplateCommand, RemoveFactorFromTemplateResult>
{
    public async Task<RemoveFactorFromTemplateResult> Handle(
        RemoveFactorFromTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Market comparable template with ID {command.TemplateId} not found.");
        }

        template.RemoveFactor(command.FactorId);
        await repository.UpdateAsync(template, cancellationToken);

        return new RemoveFactorFromTemplateResult(true);
    }
}
