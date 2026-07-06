using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.ReorderComparativeAnalysisTemplateFactors;

public record ReorderComparativeAnalysisTemplateFactorsCommand(
    Guid TemplateId,
    List<ReorderFactorItem> Factors
) : ICommand<ReorderComparativeAnalysisTemplateFactorsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
