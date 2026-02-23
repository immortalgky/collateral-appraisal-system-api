using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

/// <summary>
/// Command to update pricing analysis final values
/// </summary>
public record UpdatePricingAnalysisCommand(
    Guid Id,
    decimal MarketValue,
    decimal AppraisedValue,
    decimal? ForcedSaleValue
) : ICommand<UpdatePricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
