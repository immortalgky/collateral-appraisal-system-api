using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.DeleteComparativeAnalysisTemplate;

public class DeleteComparativeAnalysisTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<DeleteComparativeAnalysisTemplateCommand, DeleteComparativeAnalysisTemplateResult>
{
    public async Task<DeleteComparativeAnalysisTemplateResult> Handle(
        DeleteComparativeAnalysisTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        templateRepository.Delete(template);

        return new DeleteComparativeAnalysisTemplateResult(true);
    }
}
