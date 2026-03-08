using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.DeleteComparativeAnalysisTemplate;

public record DeleteComparativeAnalysisTemplateCommand(Guid TemplateId)
    : ICommand<DeleteComparativeAnalysisTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
