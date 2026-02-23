using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.DeleteTemplate;

public record DeleteTemplateCommand(Guid TemplateId)
    : ICommand<DeleteTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
