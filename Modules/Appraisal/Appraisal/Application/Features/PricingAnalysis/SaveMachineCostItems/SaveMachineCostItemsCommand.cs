using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

public record SaveMachineCostItemsCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    IReadOnlyList<MachineCostItemInput> Items,
    string? Remark = null,
    decimal? FinalValueOverride = null,
    decimal? IndicatedValue = null
) : ICommand<SaveMachineCostItemsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
