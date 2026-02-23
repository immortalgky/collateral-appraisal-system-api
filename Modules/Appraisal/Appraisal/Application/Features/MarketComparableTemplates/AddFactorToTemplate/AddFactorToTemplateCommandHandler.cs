using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.AddFactorToTemplate;

public class AddFactorToTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<AddFactorToTemplateCommand, AddFactorToTemplateResult>
{
    public async Task<AddFactorToTemplateResult> Handle(
        AddFactorToTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Market comparable template with ID {command.TemplateId} not found.");
        }

        var templateFactor = template.AddFactor(
            command.FactorId,
            command.DisplaySequence,
            command.IsMandatory);

        await repository.UpdateAsync(template, cancellationToken);

        return new AddFactorToTemplateResult(templateFactor.Id);
    }
}
