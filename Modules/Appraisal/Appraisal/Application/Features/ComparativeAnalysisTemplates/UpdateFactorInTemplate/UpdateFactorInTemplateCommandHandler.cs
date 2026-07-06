using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateFactorInTemplate;

public class UpdateFactorInTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<UpdateFactorInTemplateCommand, UpdateFactorInTemplateResult>
{
    public async Task<UpdateFactorInTemplateResult> Handle(
        UpdateFactorInTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        template.UpdateFactor(
            command.FactorId,
            command.IsMandatory,
            command.DefaultWeight,
            command.DefaultIntensity,
            command.IsCalculationFactor
        );

        templateRepository.Update(template);

        return new UpdateFactorInTemplateResult(true);
    }
}
