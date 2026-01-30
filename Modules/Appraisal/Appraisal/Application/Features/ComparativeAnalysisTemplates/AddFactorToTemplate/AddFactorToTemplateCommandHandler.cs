using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public class AddFactorToTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<AddFactorToTemplateCommand, AddFactorToTemplateResult>
{
    public async Task<AddFactorToTemplateResult> Handle(
        AddFactorToTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        var factor = template.AddFactor(
            command.FactorId,
            command.DisplaySequence,
            command.IsMandatory,
            command.DefaultWeight
        );

        templateRepository.Update(template);

        return new AddFactorToTemplateResult(
            factor.Id,
            factor.TemplateId,
            factor.FactorId,
            factor.DisplaySequence,
            factor.IsMandatory,
            factor.DefaultWeight
        );
    }
}
