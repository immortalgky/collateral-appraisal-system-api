using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateTemplate;

public record UpdateTemplateCommand(
    Guid TemplateId,
    string TemplateName,
    string? Description,
    bool? IsActive
) : ICommand<UpdateTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
