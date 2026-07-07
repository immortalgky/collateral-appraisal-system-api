using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetMarketComparableTemplateStatus;

public class SetMarketComparableTemplateStatusCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<SetMarketComparableTemplateStatusCommand, SetMarketComparableTemplateStatusResult>
{
    public async Task<SetMarketComparableTemplateStatusResult> Handle(
        SetMarketComparableTemplateStatusCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (template is null)
            throw new NotFoundException("MarketComparableTemplate", command.Id);

        if (command.IsActive)
            template.Activate();
        else
            template.Deactivate();

        await repository.UpdateAsync(template, cancellationToken);

        return new SetMarketComparableTemplateStatusResult(true);
    }
}
