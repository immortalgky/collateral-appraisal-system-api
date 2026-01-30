using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.RemoveFactorFromTemplate;

public record RemoveFactorFromTemplateCommand(
    Guid TemplateId,
    Guid FactorId
) : ICommand<RemoveFactorFromTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
