using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public class UpdateComparativeAnalysisTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<UpdateComparativeAnalysisTemplateCommand, UpdateComparativeAnalysisTemplateResult>
{
    public async Task<UpdateComparativeAnalysisTemplateResult> Handle(
        UpdateComparativeAnalysisTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new InvalidOperationException($"Template {command.TemplateId} not found");

        template.Update(command.TemplateName, command.Description);

        if (command.IsActive.HasValue)
        {
            if (command.IsActive.Value)
                template.Activate();
            else
                template.Deactivate();
        }

        templateRepository.Update(template);

        return new UpdateComparativeAnalysisTemplateResult(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.PropertyType,
            template.Description,
            template.IsActive
        );
    }
}
