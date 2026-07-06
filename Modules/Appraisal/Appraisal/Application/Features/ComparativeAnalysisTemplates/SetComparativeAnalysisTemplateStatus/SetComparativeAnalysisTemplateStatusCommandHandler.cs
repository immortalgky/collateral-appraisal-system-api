using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.SetComparativeAnalysisTemplateStatus;

public class SetComparativeAnalysisTemplateStatusCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<SetComparativeAnalysisTemplateStatusCommand, SetComparativeAnalysisTemplateStatusResult>
{
    public async Task<SetComparativeAnalysisTemplateStatusResult> Handle(
        SetComparativeAnalysisTemplateStatusCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(command.Id, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.Id} not found");

        if (command.IsActive)
            template.Activate();
        else
            template.Deactivate();

        templateRepository.Update(template);

        return new SetComparativeAnalysisTemplateStatusResult(true);
    }
}
