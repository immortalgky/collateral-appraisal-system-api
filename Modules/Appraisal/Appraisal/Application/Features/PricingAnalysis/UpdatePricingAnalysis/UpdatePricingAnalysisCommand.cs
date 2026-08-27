using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

/// <summary>
/// Command to update pricing analysis final values and/or its calculation mode.
/// Null means "leave alone" for every field.
/// </summary>
public record UpdatePricingAnalysisCommand(
    Guid Id,
    decimal? MarketValue,
    decimal? AppraisedValue,
    decimal? ForcedSaleValue,
    bool? UseSystemCalc
) : ICommand<UpdatePricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
