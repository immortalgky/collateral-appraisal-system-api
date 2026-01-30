using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.RemoveFactorFromTemplate;

public class RemoveFactorFromTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<RemoveFactorFromTemplateCommand, RemoveFactorFromTemplateResult>
{
    public async Task<RemoveFactorFromTemplateResult> Handle(
        RemoveFactorFromTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        template.RemoveFactor(command.FactorId);
        templateRepository.Update(template);

        return new RemoveFactorFromTemplateResult(true);
    }
}
