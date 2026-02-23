using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.CreateMarketComparableTemplate;

public class CreateMarketComparableTemplateCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<CreateMarketComparableTemplateCommand, CreateMarketComparableTemplateResult>
{
    public async Task<CreateMarketComparableTemplateResult> Handle(
        CreateMarketComparableTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = MarketComparableTemplate.Create(
            command.TemplateCode,
            command.TemplateName,
            command.PropertyType,
            command.Description);

        await repository.AddAsync(template, cancellationToken);

        return new CreateMarketComparableTemplateResult(template.Id);
    }
}
