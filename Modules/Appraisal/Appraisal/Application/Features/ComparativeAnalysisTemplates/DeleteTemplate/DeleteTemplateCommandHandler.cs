using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.DeleteTemplate;

public class DeleteTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<DeleteTemplateCommand, DeleteTemplateResult>
{
    public async Task<DeleteTemplateResult> Handle(
        DeleteTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        templateRepository.Delete(template);

        return new DeleteTemplateResult(true);
    }
}
