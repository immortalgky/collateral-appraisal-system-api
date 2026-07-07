using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.SetComparativeAnalysisTemplateStatus;

public record SetComparativeAnalysisTemplateStatusCommand(
    Guid Id,
    bool IsActive
) : ICommand<SetComparativeAnalysisTemplateStatusResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
