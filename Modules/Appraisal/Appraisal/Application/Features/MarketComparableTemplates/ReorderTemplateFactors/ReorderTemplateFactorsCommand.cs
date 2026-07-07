using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.ReorderTemplateFactors;

public record ReorderTemplateFactorsCommand(
    Guid TemplateId,
    List<ReorderFactorItem> Factors
) : ICommand<ReorderTemplateFactorsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
