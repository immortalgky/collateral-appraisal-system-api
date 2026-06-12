namespace Appraisal.Application.Features.MarketComparableTemplates.RemoveFactorFromTemplate;

public class RemoveFactorFromTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<RemoveFactorFromTemplateCommand, RemoveFactorFromTemplateResult>
{
    public async Task<RemoveFactorFromTemplateResult> Handle(
        RemoveFactorFromTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new NotFoundException("MarketComparableTemplate", command.TemplateId);

        template.RemoveFactor(command.FactorId);

        return new RemoveFactorFromTemplateResult(true);
    }
}