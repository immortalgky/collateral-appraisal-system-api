using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateTemplate;

public record CreateTemplateCommand(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description = null
) : ICommand<CreateTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
