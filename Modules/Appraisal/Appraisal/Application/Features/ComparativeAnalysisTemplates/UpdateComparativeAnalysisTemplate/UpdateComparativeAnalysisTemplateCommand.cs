using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateComparativeAnalysisTemplate;

public record UpdateComparativeAnalysisTemplateCommand(
    Guid TemplateId,
    string TemplateName,
    string? Description,
    bool? IsActive
) : ICommand<UpdateComparativeAnalysisTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
