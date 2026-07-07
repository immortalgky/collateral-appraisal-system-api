using Appraisal.Domain.MarketComparables;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetTemplateFactorMandatory;

public class SetTemplateFactorMandatoryCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<SetTemplateFactorMandatoryCommand, SetTemplateFactorMandatoryResult>
{
    public async Task<SetTemplateFactorMandatoryResult> Handle(
        SetTemplateFactorMandatoryCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new NotFoundException("MarketComparableTemplate", command.TemplateId);

        template.SetFactorMandatory(command.FactorId, command.IsMandatory);

        return new SetTemplateFactorMandatoryResult(true);
    }
}
