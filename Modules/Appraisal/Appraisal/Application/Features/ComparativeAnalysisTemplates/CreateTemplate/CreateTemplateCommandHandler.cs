using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public class CreateTemplateCommandHandler(
    IComparativeAnalysisTemplateRepository templateRepository
) : ICommandHandler<CreateTemplateCommand, CreateTemplateResult>
{
    public async Task<CreateTemplateResult> Handle(
        CreateTemplateCommand command,
        CancellationToken cancellationToken)
    {
        // Check for duplicate template code
        if (await templateRepository.ExistsByTemplateCodeAsync(command.TemplateCode, cancellationToken))
            throw new InvalidOperationException($"Template with code '{command.TemplateCode}' already exists");

        // Create template using domain factory
        var template = ComparativeAnalysisTemplate.Create(
            command.TemplateCode,
            command.TemplateName,
            command.PropertyType,
            command.Description
        );

        templateRepository.Add(template);

        return new CreateTemplateResult(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.PropertyType,
            template.Description,
            template.IsActive
        );
    }
}
