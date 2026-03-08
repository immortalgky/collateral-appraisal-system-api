using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.CreateComparativeAnalysisTemplate;

public record CreateComparativeAnalysisTemplateCommand(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description = null
) : ICommand<CreateComparativeAnalysisTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
